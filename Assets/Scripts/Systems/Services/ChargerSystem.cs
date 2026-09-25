using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Electric cars skip the pumps: they take a free charger, charge for 20–40 game minutes while the driver
    /// visits the shop and the diner, pay for the kWh and leave. With no free charger they wait (and may give up).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(ShopSystem))]
    [UpdateBefore(typeof(ParkingSystem))]
    public partial struct ChargerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChargingStation>();
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stationEntity = SystemAPI.GetSingletonEntity<ChargingStation>();
            ref var station = ref SystemAPI.GetComponentRW<ChargingStation>(stationEntity).ValueRW;
            var spots = SystemAPI.GetBuffer<ChargerSpot>(stationEntity);
            int open = EvMath.OpenChargers(SystemAPI.GetSingleton<StationUpgrades>().EvCharger, spots.Length);
            float minutesPerSecond = SystemAPI.GetSingleton<GameTime>().MinutesPerSecond;
            bool hasShop = SystemAPI.HasSingleton<Shop>();
            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>() ? SystemAPI.GetSingleton<StationCleanliness>().Value : 1f;
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot.Occupant != Entity.Null && !SystemAPI.Exists(spot.Occupant))
                {
                    spot.Occupant = Entity.Null;
                    spots[i] = spot;
                }
            }

            foreach (var (car, path, transform, patience, entity) in SystemAPI
                         .Query<RefRW<Car>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>, RefRO<Patience>>()
                         .WithEntityAccess())
            {
                if (car.ValueRO.Customer != CustomerType.Electric)
                    continue;

                switch (car.ValueRO.State)
                {
                    case CarState.Queued:
                    {
                        int free = FreeSpot(spots, open);
                        if (free < 0)
                            break;

                        var spot = spots[free];
                        spot.Occupant = entity;
                        spots[free] = spot;
                        car.ValueRW.State = CarState.DrivingToCharger;
                        path.Clear();
                        path.Add(new PathPoint { Position = spot.Position });
                        float minutes = station.Random.NextFloat(EvMath.MinMinutes, EvMath.MaxMinutes);
                        ecb.AddComponent(entity, new EvCharge
                        {
                            Spot = free,
                            TimeLeft = EvMath.ChargeSeconds(minutes, minutesPerSecond),
                            Kwh = station.Random.NextFloat(EvMath.MinKwh, EvMath.MaxKwh)
                        });
                        break;
                    }

                    case CarState.DrivingToCharger:
                        if (!path.IsEmpty || !SystemAPI.HasComponent<EvCharge>(entity))
                            break;
                        car.ValueRW.State = CarState.Charging;
                        transform.ValueRW.Rotation = spots[SystemAPI.GetComponent<EvCharge>(entity).Spot].Rotation;
                        break;

                    case CarState.Charging:
                    case CarState.Shopping:
                    case CarState.ReadyToLeave:
                    {
                        if (!SystemAPI.HasComponent<EvCharge>(entity))
                            break;

                        var charge = SystemAPI.GetComponentRW<EvCharge>(entity);
                        charge.ValueRW.TimeLeft -= deltaTime;

                        // The driver goes shopping (and eating) once while the car charges.
                        if (car.ValueRO.State == CarState.Charging && !charge.ValueRO.Shopped && hasShop)
                        {
                            charge.ValueRW.Shopped = true;
                            car.ValueRW.State = CarState.Shopping;
                            break;
                        }

                        if (car.ValueRO.State == CarState.ReadyToLeave && charge.ValueRO.TimeLeft > 0f)
                            car.ValueRW.State = CarState.Charging;

                        if (car.ValueRO.State == CarState.Shopping || charge.ValueRO.TimeLeft > 0f)
                            break;

                        Finish(ref car.ValueRW, path, spots, charge.ValueRO, station.PricePerKwh, patience.ValueRO, cleanliness,
                            exitRoute, events, ref economy);
                        break;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int FreeSpot(DynamicBuffer<ChargerSpot> spots, int open)
        {
            for (int i = 0; i < open && i < spots.Length; i++)
            {
                if (spots[i].Occupant == Entity.Null)
                    return i;
            }

            return -1;
        }

        private static void Finish(ref Car car, DynamicBuffer<PathPoint> path, DynamicBuffer<ChargerSpot> spots, in EvCharge charge,
            float pricePerKwh, in Patience patience, float cleanliness, DynamicBuffer<ExitRoutePoint> exitRoute,
            DynamicBuffer<StationEvent> events, ref Economy economy)
        {
            float bill = EvMath.Bill(charge.Kwh, pricePerKwh);
            economy.Money += bill;
            economy.DayIncome += bill;
            economy.DayServed++;
            StationEvent.Push(events, StationEventType.EvCharged, default, bill, (int)charge.Kwh);

            float patienceRatio = patience.Max > 0f ? patience.Current / patience.Max : 1f;
            StationEvent.Push(events, StationEventType.CustomerReview, default,
                ReviewMath.Stars(patienceRatio, pricePerKwh, pricePerKwh, cleanliness));

            if (charge.Spot >= 0 && charge.Spot < spots.Length)
            {
                var spot = spots[charge.Spot];
                spot.Occupant = Entity.Null;
                spots[charge.Spot] = spot;
            }

            // Drive straight to the turn-out: the first exit point is back by the pumps.
            car.State = CarState.Leaving;
            path.Clear();
            for (int i = exitRoute.Length > 1 ? 1 : 0; i < exitRoute.Length; i++)
                path.Add(new PathPoint { Position = exitRoute[i].Position });
        }
    }
}
