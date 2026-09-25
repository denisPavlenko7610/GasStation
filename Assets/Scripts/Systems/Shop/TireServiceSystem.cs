using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Cars that need tires go to the tire bay after the pump (if the service is open and free).
    /// The player starts the job with interact next to the bay; a hired mechanic starts it on his own.
    /// A customer left waiting too long drives away without paying.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(ParkingSystem))]
    [UpdateBefore(typeof(CarWashSystem))]
    public partial struct TireServiceSystem : ISystem
    {
        private const float MechanicStartDelay = 4f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TireService>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var upgrades = SystemAPI.GetSingleton<StationUpgrades>();
            int level = upgrades.TireService;
            var serviceEntity = SystemAPI.GetSingletonEntity<TireService>();
            ref var service = ref SystemAPI.GetComponentRW<TireService>(serviceEntity).ValueRW;
            var entryRoute = SystemAPI.GetBuffer<TireEntryPoint>(serviceEntity);
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;

            // Only take the interact press when there is work to start, so it can still pick up litter.
            bool carWaiting = !IsFree(ref state, service.Occupant) &&
                              SystemAPI.HasComponent<Car>(service.Occupant) &&
                              SystemAPI.GetComponent<Car>(service.Occupant).State == CarState.WaitingForTires;
            bool playerStartsJob = carWaiting && PlayerPressedAtBay(ref state, service.Bay);

            foreach (var (car, patience, path, transform, entity) in SystemAPI
                         .Query<RefRW<Car>, RefRW<Patience>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (car.ValueRO.State)
                {
                    case CarState.ReadyToLeave:
                    {
                        if (!car.ValueRO.NeedsTires || level == 0 || !IsFree(ref state, service.Occupant))
                            break;

                        var pumpEntity = car.ValueRO.Pump;
                        if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                        {
                            var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                            if (pump.ValueRO.Occupant == entity)
                                pump.ValueRW.Occupant = Entity.Null;
                        }

                        service.Occupant = entity;
                        car.ValueRW.State = CarState.DrivingToTires;
                        car.ValueRW.Pump = Entity.Null;
                        path.Clear();
                        for (int i = 0; i < entryRoute.Length; i++)
                            path.Add(new PathPoint { Position = entryRoute[i].Position });
                        path.Add(new PathPoint { Position = service.Bay });
                        break;
                    }

                    case CarState.DrivingToTires:
                        if (!path.IsEmpty)
                            break;

                        car.ValueRW.State = CarState.WaitingForTires;
                        car.ValueRW.ServiceWait = 0f;
                        transform.ValueRW.Rotation = service.BayRotation;
                        break;

                    case CarState.WaitingForTires:
                    {
                        car.ValueRW.ServiceWait += deltaTime;
                        bool mechanicStarts = upgrades.Mechanic > 0 && car.ValueRO.ServiceWait >= MechanicStartDelay;
                        if (playerStartsJob || mechanicStarts)
                        {
                            playerStartsJob = false;
                            car.ValueRW.State = CarState.ChangingTires;
                            car.ValueRW.Timer = ShopMath.TireDuration(service.Duration, level);
                            break;
                        }

                        patience.ValueRW.Current -= deltaTime;
                        if (patience.ValueRO.Current > 0f)
                            break;

                        // Gave up waiting: no tires, no money.
                        service.Occupant = Entity.Null;
                        car.ValueRW.NeedsTires = false;
                        economy.DayLost++;
                        economy.Reputation = StationMath.ClampReputation(economy.Reputation - StationMath.LostCustomerPenalty);
                        StationEvent.Push(events, StationEventType.CustomerLeftAngry);
                        CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
                        break;
                    }

                    case CarState.ChangingTires:
                    {
                        car.ValueRW.Timer -= deltaTime;
                        if (car.ValueRO.Timer > 0f)
                            break;

                        float price = ShopMath.TirePrice(service.Price, level);
                        economy.Money += price;
                        economy.DayIncome += price;
                        StationEvent.Push(events, StationEventType.TiresChanged, default, price);

                        service.Occupant = Entity.Null;
                        car.ValueRW.NeedsTires = false;
                        // Back to the departure decision: the wash or the road.
                        car.ValueRW.State = CarState.ReadyToLeave;
                        break;
                    }
                }
            }
        }

        private bool PlayerPressedAtBay(ref SystemState state, float3 bay)
        {
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                if (!interaction.ValueRO.InteractPressed)
                    continue;
                if (math.distancesq(transform.ValueRO.Position.xz, bay.xz) > radius * radius)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                return true;
            }

            return false;
        }

        private bool IsFree(ref SystemState state, Entity occupant) =>
            occupant == Entity.Null || !SystemAPI.Exists(occupant);
    }
}
