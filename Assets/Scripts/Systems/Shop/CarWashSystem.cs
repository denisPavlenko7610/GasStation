using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Frees the pump of cars that are done and sends them to the wash (if open, free and wanted) or away;
    /// runs the wash bay.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(ShopSystem))]
    public partial struct CarWashSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            int washLevel = SystemAPI.GetSingleton<StationUpgrades>().CarWash;

            bool hasWash = SystemAPI.HasSingleton<CarWash>();
            var washEntity = hasWash ? SystemAPI.GetSingletonEntity<CarWash>() : Entity.Null;

            foreach (var (car, path, transform, entity) in SystemAPI
                         .Query<RefRW<Car>, DynamicBuffer<PathPoint>, RefRW<Unity.Transforms.LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (car.ValueRO.State)
                {
                    case CarState.ReadyToLeave:
                    {
                        var pumpEntity = car.ValueRO.Pump;
                        if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                        {
                            var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                            if (pump.ValueRO.Occupant == entity)
                                pump.ValueRW.Occupant = Entity.Null;
                        }

                        if (hasWash && washLevel > 0 && car.ValueRO.WantsWash && IsFree(ref state, washEntity))
                        {
                            var wash = SystemAPI.GetComponentRW<CarWash>(washEntity);
                            wash.ValueRW.Occupant = entity;
                            car.ValueRW.State = CarState.DrivingToWash;
                            car.ValueRW.Pump = Entity.Null;

                            path.Clear();
                            var entry = SystemAPI.GetBuffer<WashEntryPoint>(washEntity);
                            for (int i = 0; i < entry.Length; i++)
                                path.Add(new PathPoint { Position = entry[i].Position });
                            path.Add(new PathPoint { Position = wash.ValueRO.Bay });
                        }
                        else
                        {
                            CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
                        }

                        break;
                    }

                    case CarState.DrivingToWash:
                        if (!path.IsEmpty || !hasWash)
                            break;

                        var bay = SystemAPI.GetComponent<CarWash>(washEntity);
                        car.ValueRW.State = CarState.Washing;
                        car.ValueRW.Timer = ShopMath.WashDuration(bay.Duration, washLevel);
                        transform.ValueRW.Rotation = bay.BayRotation;
                        break;

                    case CarState.Washing:
                    {
                        car.ValueRW.Timer -= deltaTime;
                        if (car.ValueRO.Timer > 0f || !hasWash)
                            break;

                        var wash = SystemAPI.GetComponentRW<CarWash>(washEntity);
                        float price = ShopMath.WashPrice(wash.ValueRO.Price, washLevel);
                        economy.Money += price;
                        economy.DayIncome += price;
                        StationEvent.Push(events, StationEventType.CarWashed, default, price);
                        wash.ValueRW.Occupant = Entity.Null;

                        car.ValueRW.State = CarState.Leaving;
                        path.Clear();
                        var exit = SystemAPI.GetBuffer<WashExitPoint>(washEntity);
                        for (int i = 0; i < exit.Length; i++)
                            path.Add(new PathPoint { Position = exit[i].Position });
                        break;
                    }
                }
            }
        }

        private bool IsFree(ref SystemState state, Entity washEntity)
        {
            var occupant = SystemAPI.GetComponent<CarWash>(washEntity).Occupant;
            return occupant == Entity.Null || !SystemAPI.Exists(occupant);
        }
    }
}
