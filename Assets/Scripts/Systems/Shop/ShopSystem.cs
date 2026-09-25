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
            float cashiers = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>().Cashier : 0f;
            float shopTime = shop.ShopTime * StaffMath.ShopTimeFactor(cashiers);
            float hour = SystemAPI.HasSingleton<GameTime>() ? SystemAPI.GetSingleton<GameTime>().Hour : 12f;
            var watch = new TheftWatch
            {
                Cashiers = cashiers,
                Cameras = CamerasNear(ref state, shop.Door),
                PlayerAtDoor = PlayerNear(ref state, shop.Door)
            };

            foreach (var (car, transform, entity) in SystemAPI.Query<RefRW<Car>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (car.ValueRO.State != CarState.Shopping)
                    continue;

                if (!car.ValueRO.DriverAway)
                {
                    car.ValueRW.DriverAway = true;
                    if (shop.PedestrianPrefab != Entity.Null)
                    {
                        // The driver plus any passengers (a tour bus unloads a crowd).
                        int people = 1 + car.ValueRO.Passengers;
                        car.ValueRW.PeopleAway = (byte)people;
                        for (int i = 0; i < people; i++)
                        {
                            var who = i == 0 ? car.ValueRO.Customer : CustomerType.Tourist;
                            bool shoplifter = shop.Random.NextFloat() < ShopMath.ShopliftChance(who);
                            if (shoplifter)
                                StationEvent.Push(events, StationEventType.SuspiciousCustomer);
                            SpawnDriver(ecb, shop, entity, transform.ValueRO, i, shoplifter);
                        }
                    }
                    else
                    {
                        float walk = math.distance(transform.ValueRO.Position, shop.Door) / WalkSpeed;
                        car.ValueRW.Timer = shopTime + 2f * walk;
                    }

                    continue;
                }

                if (shop.PedestrianPrefab != Entity.Null)
                    continue;

                car.ValueRW.Timer -= deltaTime;
                if (car.ValueRO.Timer > 0f)
                    continue;

                for (int i = 0; i <= car.ValueRO.Passengers; i++)
                {
                    var who = i == 0 ? car.ValueRO.Customer : CustomerType.Tourist;
                    if (shop.Random.NextFloat() < ShopMath.ShopliftChance(who))
                        Shoplift(ref shop, shelves, ref economy, events, who, watch);
                    else
                        Purchase(ref shop, shelves, ref economy, events, who);
                    DinerSale(ref state, ref shop, ref economy, events, who, hour);
                    UseRestroom(ref state, ref shop, ref economy, events);
                }

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
                            pedestrian.ValueRW.Timer = shopTime;
                        }
                        break;

                    case PedestrianState.InShop:
                        pedestrian.ValueRW.Timer -= deltaTime;
                        if (pedestrian.ValueRO.Timer > 0f)
                            break;

                        var customer = SystemAPI.GetComponent<Car>(carEntity).Customer;
                        if (pedestrian.ValueRO.Shoplifter)
                            Shoplift(ref shop, shelves, ref economy, events, customer, watch);
                        else
                            Purchase(ref shop, shelves, ref economy, events, customer);
                        DinerSale(ref state, ref shop, ref economy, events, customer, hour);
                        UseRestroom(ref state, ref shop, ref economy, events);
                        pedestrian.ValueRW.State = PedestrianState.ToCar;
                        path.Add(new PathPoint { Position = DoorOfCar(SystemAPI.GetComponent<LocalTransform>(carEntity)) });
                        break;

                    case PedestrianState.ToCar:
                        if (!path.IsEmpty)
                            break;

                        var car = SystemAPI.GetComponentRW<Car>(carEntity);
                        ecb.DestroyEntity(entity);
                        // Everyone has to be back on board before the car leaves.
                        if (car.ValueRO.PeopleAway > 1)
                        {
                            car.ValueRW.PeopleAway--;
                            break;
                        }

                        car.ValueRW.PeopleAway = 0;
                        car.ValueRW.DriverAway = false;
                        car.ValueRW.State = CarState.ReadyToLeave;
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>Hungry visitors grab a hot dog or a burger from the diner counter.</summary>
        private void DinerSale(ref SystemState state, ref Shop shop, ref Economy economy, DynamicBuffer<StationEvent> events,
            CustomerType customer, float hour)
        {
            if (!SystemAPI.HasSingleton<Diner>() || !SystemAPI.HasSingleton<StationUpgrades>() ||
                SystemAPI.GetSingleton<StationUpgrades>().Diner <= 0)
                return;
            if (shop.Random.NextFloat() >= DinerMath.HungerChance(customer, hour))
                return;

            var counter = SystemAPI.GetBuffer<DinerCounter>(SystemAPI.GetSingletonEntity<Diner>());
            if (counter.Length < DinerDishes.Count)
                return;

            int dish = DinerMath.Choose(counter[0].Ready, counter[1].Ready, shop.Random.NextFloat());
            if (dish < 0)
                return;

            var item = counter[dish];
            item.Ready--;
            counter[dish] = item;
            economy.Money += item.Price;
            economy.DayIncome += item.Price;
            StationEvent.Push(events, StationEventType.DinerSale, default, item.Price, dish);
        }

        private struct TheftWatch
        {
            public float Cashiers;
            public int Cameras;
            public bool PlayerAtDoor;
        }

        /// <summary>
        /// A shoplifter at the till: caught (they pay after all) or gone with 1–3 items. Insurance covers most of
        /// the loss (FinanceSystem).
        /// </summary>
        private static void Shoplift(ref Shop shop, DynamicBuffer<ShopProduct> shelves, ref Economy economy,
            DynamicBuffer<StationEvent> events, CustomerType customer, in TheftWatch watch)
        {
            if (shop.Random.NextFloat() < ShopMath.ShopliftCatchChance(watch.Cashiers, watch.Cameras, watch.PlayerAtDoor))
            {
                StationEvent.Push(events, StationEventType.ShoplifterCaught);
                Purchase(ref shop, shelves, ref economy, events, customer);
                return;
            }

            if (shelves.Length < ProductTypes.Count)
                return;

            float worth = 0f;
            int items = shop.Random.NextInt(1, 4);
            for (int i = 0; i < items; i++)
            {
                int index = ShopMath.Pick(shop.Random.NextFloat(), customer, shelves[0], shelves[1], shelves[2], shelves[3], shelves[4]);
                if (index < 0)
                    break;
                var shelf = shelves[index];
                shelf.Stock--;
                shelves[index] = shelf;
                worth += shelf.SellPrice;
            }

            if (worth > 0f)
                StationEvent.Push(events, StationEventType.GoodsStolen, default, worth);
        }

        private int CamerasNear(ref SystemState state, float3 point)
        {
            float radius = PropMath.Get(PropType.SecurityCamera).Radius;
            int count = 0;
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
            {
                if (prop.ValueRO.Type == PropType.SecurityCamera && math.distancesq(prop.ValueRO.Position.xz, point.xz) <= radius * radius)
                    count++;
            }

            return count;
        }

        private bool PlayerNear(ref SystemState state, float3 point)
        {
            const float radius = 4f;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                if (math.distancesq(transform.ValueRO.Position.xz, point.xz) <= radius * radius)
                    return true;
            }

            return false;
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

        private static void SpawnDriver(EntityCommandBuffer ecb, Shop shop, Entity car, LocalTransform carTransform, int index,
            bool shoplifter)
        {
            // Passengers step out one behind the other along the car.
            float3 back = math.mul(carTransform.Rotation, new float3(0f, 0f, -1f));
            float3 start = DoorOfCar(carTransform) + back * (0.7f * index);
            var driver = ecb.Instantiate(shop.PedestrianPrefab);
            ecb.AddComponent(driver, LocalTransform.FromPosition(start));
            ecb.AddComponent(driver, new Pedestrian { State = PedestrianState.ToShop, Car = car, Shoplifter = shoplifter });
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
                shelf.Stock -= ShopMath.UnitsPerSale(shelf);
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
