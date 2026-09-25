using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Drivers of cars in the Shopping state walk to the shop, buy a few products and walk back.
    /// Without a pedestrian prefab the trip is simulated with a timer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(FuelingSystem))]
    public partial struct ShopSystem : ISystem
    {
        private const float WalkSpeed = 1.8f;
        private const float SideOffset = 1.8f;
        private const float EmptyShopPenalty = 0.01f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Shop>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var shopEntity = SystemAPI.GetSingletonEntity<Shop>();
            ref var shop = ref SystemAPI.GetComponentRW<Shop>(shopEntity).ValueRW;
            var shelves = SystemAPI.GetBuffer<ShopProduct>(shopEntity);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (car, transform, entity) in SystemAPI.Query<RefRW<Car>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (car.ValueRO.State != CarState.Shopping)
                    continue;

                if (!car.ValueRO.DriverAway)
                {
                    car.ValueRW.DriverAway = true;
                    if (shop.PedestrianPrefab != Entity.Null)
                    {
                        SpawnDriver(ecb, shop, entity, transform.ValueRO);
                    }
                    else
                    {
                        float walk = math.distance(transform.ValueRO.Position, shop.Door) / WalkSpeed;
                        car.ValueRW.Timer = shop.ShopTime + 2f * walk;
                    }

                    continue;
                }

                if (shop.PedestrianPrefab != Entity.Null)
                    continue;

                car.ValueRW.Timer -= deltaTime;
                if (car.ValueRO.Timer > 0f)
                    continue;

                Purchase(ref shop, shelves, ref economy, events, car.ValueRO.Customer);
                UseRestroom(ref state, ref shop, ref economy, events);
                car.ValueRW.DriverAway = false;
                car.ValueRW.State = CarState.ReadyToLeave;
            }

            foreach (var (pedestrian, path, entity) in SystemAPI.Query<RefRW<Pedestrian>, DynamicBuffer<PathPoint>>().WithEntityAccess())
            {
                var carEntity = pedestrian.ValueRO.Car;
                if (!SystemAPI.Exists(carEntity) || !SystemAPI.HasComponent<Car>(carEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                switch (pedestrian.ValueRO.State)
                {
                    case PedestrianState.ToShop:
                        if (path.IsEmpty)
                        {
                            pedestrian.ValueRW.State = PedestrianState.InShop;
                            pedestrian.ValueRW.Timer = shop.ShopTime;
                        }
                        break;

                    case PedestrianState.InShop:
                        pedestrian.ValueRW.Timer -= deltaTime;
                        if (pedestrian.ValueRO.Timer > 0f)
                            break;

                        Purchase(ref shop, shelves, ref economy, events, SystemAPI.GetComponent<Car>(carEntity).Customer);
                        UseRestroom(ref state, ref shop, ref economy, events);
                        pedestrian.ValueRW.State = PedestrianState.ToCar;
                        path.Add(new PathPoint { Position = DoorOfCar(SystemAPI.GetComponent<LocalTransform>(carEntity)) });
                        break;

                    case PedestrianState.ToCar:
                        if (!path.IsEmpty)
                            break;

                        var car = SystemAPI.GetComponentRW<Car>(carEntity);
                        car.ValueRW.DriverAway = false;
                        car.ValueRW.State = CarState.ReadyToLeave;
                        ecb.DestroyEntity(entity);
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>Some shop visitors also use the restroom; a disgusting one costs reputation.</summary>
        private void UseRestroom(ref SystemState state, ref Shop shop, ref Economy economy, DynamicBuffer<StationEvent> events)
        {
            if (!SystemAPI.HasSingleton<Restroom>())
                return;

            ref var restroom = ref SystemAPI.GetSingletonRW<Restroom>().ValueRW;
            if (shop.Random.NextFloat() >= restroom.VisitChance)
                return;

            restroom.Dirt = math.min(1f, restroom.Dirt + restroom.DirtPerVisit);
            StationEvent.Push(events, StationEventType.RestroomUsed);
            if (restroom.Dirt >= FacilityMath.RestroomDisgustingDirt)
            {
                economy.Reputation = StationMath.ClampReputation(economy.Reputation - EmptyShopPenalty);
                StationEvent.Push(events, StationEventType.RestroomDisgusting);
            }
        }

        private static void SpawnDriver(EntityCommandBuffer ecb, Shop shop, Entity car, LocalTransform carTransform)
        {
            float3 start = DoorOfCar(carTransform);
            var driver = ecb.Instantiate(shop.PedestrianPrefab);
            ecb.AddComponent(driver, LocalTransform.FromPosition(start));
            ecb.AddComponent(driver, new Pedestrian { State = PedestrianState.ToShop, Car = car });
            ecb.AddComponent(driver, new CarMovement { Speed = WalkSpeed, TurnSpeed = 8f });
            var path = ecb.AddBuffer<PathPoint>(driver);
            path.Add(new PathPoint { Position = shop.Door });
        }

        /// <summary>A point next to the driver's door, on the car's left side.</summary>
        private static float3 DoorOfCar(LocalTransform carTransform)
        {
            float3 left = math.mul(carTransform.Rotation, new float3(-1f, 0f, 0f));
            return carTransform.Position + left * SideOffset;
        }

        private static void Purchase(ref Shop shop, DynamicBuffer<ShopProduct> shelves, ref Economy economy,
            DynamicBuffer<StationEvent> events, CustomerType customer)
        {
            if (shelves.Length < ProductTypes.Count)
                return;

            int items = shop.Random.NextInt(1, 4);
            int bought = 0;
            for (int i = 0; i < items; i++)
            {
                int index = ShopMath.Pick(shop.Random.NextFloat(), customer, shelves[0], shelves[1], shelves[2], shelves[3], shelves[4]);
                if (index < 0)
                    break;

                var shelf = shelves[index];
                shelf.Stock--;
                shelves[index] = shelf;
                economy.Money += shelf.SellPrice;
                economy.DayIncome += shelf.SellPrice;
                StationEvent.Push(events, StationEventType.ShopSale, default, shelf.SellPrice);
                bought++;
            }

            if (bought == 0)
            {
                economy.Reputation = StationMath.ClampReputation(economy.Reputation - EmptyShopPenalty);
                StationEvent.Push(events, StationEventType.ShopEmpty);
            }
        }
    }
}
