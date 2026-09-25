using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// In the evening truckers who are done at the pump drive to a free parking spot, sleep until morning
    /// and pay for the night when they leave.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(ShopSystem))]
    [UpdateBefore(typeof(CarWashSystem))]
    public partial struct ParkingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TruckParking>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float hour = SystemAPI.GetSingleton<GameTime>().Hour;
            int level = SystemAPI.GetSingleton<StationUpgrades>().TruckParking;
            var parkingEntity = SystemAPI.GetSingletonEntity<TruckParking>();
            ref var parking = ref SystemAPI.GetComponentRW<TruckParking>(parkingEntity).ValueRW;
            var spots = SystemAPI.GetBuffer<ParkingSpot>(parkingEntity);
            int openSpots = FacilityMath.OpenSpots(level, spots.Length);
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;

            foreach (var (car, path, transform, entity) in SystemAPI
                         .Query<RefRW<Car>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (car.ValueRO.State)
                {
                    case CarState.ReadyToLeave:
                    {
                        if (car.ValueRO.Customer != CustomerType.Trucker || !FacilityMath.WantsToSleep(hour))
                            break;

                        int spot = FindFreeSpot(ref state, spots, openSpots);
                        if (spot < 0)
                            break;

                        var pumpEntity = car.ValueRO.Pump;
                        if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                        {
                            var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                            if (pump.ValueRO.Occupant == entity)
                                pump.ValueRW.Occupant = Entity.Null;
                        }

                        var reserved = spots[spot];
                        reserved.Occupant = entity;
                        spots[spot] = reserved;

                        car.ValueRW.State = CarState.DrivingToParking;
                        car.ValueRW.Pump = Entity.Null;
                        car.ValueRW.ParkingSpot = spot;
                        car.ValueRW.ParkUntilHour = parking.Random.NextFloat(5f, 8f);
                        path.Clear();
                        path.Add(new PathPoint { Position = parking.Entry });
                        path.Add(new PathPoint { Position = reserved.Position });
                        StationEvent.Push(events, StationEventType.TruckParked);
                        break;
                    }

                    case CarState.DrivingToParking:
                        if (!path.IsEmpty)
                            break;

                        car.ValueRW.State = CarState.Parked;
                        if (car.ValueRO.ParkingSpot < spots.Length)
                            transform.ValueRW.Rotation = spots[car.ValueRO.ParkingSpot].Rotation;
                        break;

                    case CarState.Parked:
                    {
                        if (!FacilityMath.ShouldLeaveParking(hour, car.ValueRO.ParkUntilHour))
                            break;

                        float fee = FacilityMath.ParkingFee(parking.FeePerNight, level);
                        economy.Money += fee;
                        economy.DayIncome += fee;
                        StationEvent.Push(events, StationEventType.ParkingPaid, default, fee);

                        int spot = car.ValueRO.ParkingSpot;
                        if (spot >= 0 && spot < spots.Length && spots[spot].Occupant == entity)
                        {
                            var freed = spots[spot];
                            freed.Occupant = Entity.Null;
                            spots[spot] = freed;
                        }

                        CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
                        break;
                    }
                }
            }
        }

        private int FindFreeSpot(ref SystemState state, DynamicBuffer<ParkingSpot> spots, int openSpots)
        {
            for (int i = 0; i < openSpots; i++)
            {
                var occupant = spots[i].Occupant;
                if (occupant == Entity.Null || !SystemAPI.Exists(occupant))
                    return i;
            }

            return -1;
        }
    }
}
