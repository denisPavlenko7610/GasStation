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
    /// Spawns customers. Traffic depends on time of day, reputation, prices, cleanliness, station level,
    /// upgrades and world events; the customer type depends on the station level.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(FuelDeliverySystem))]
    public partial struct CarSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<FuelStock>();
            state.RequireForUpdate<StationUpgrades>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnerEntity = SystemAPI.GetSingletonEntity<CarSpawner>();
            var prefabs = SystemAPI.GetBuffer<CarPrefabElement>(spawnerEntity);
            if (prefabs.Length == 0)
                return;

            float hour = SystemAPI.GetSingleton<GameTime>().Hour;
            float reputation = SystemAPI.GetSingleton<Economy>().Reputation;
            var upgrades = SystemAPI.GetSingleton<StationUpgrades>();
            int stationLevel = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            var worldEvent = SystemAPI.HasSingleton<WorldEvents>() ? SystemAPI.GetSingleton<WorldEvents>().Active : WorldEventKind.None;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);

            float attractiveness = 0f;
            for (int i = 0; i < stock.Length; i++)
                attractiveness += StationMath.PriceAttractiveness(stock[i].SellPrice, stock[i].MarketPrice);
            attractiveness /= stock.Length;

            float intensity = StationMath.TrafficIntensity(hour)
                              * StationMath.ReputationFactor(reputation)
                              * attractiveness
                              * UpgradeMath.TrafficMultiplier(upgrades.Advertising)
                              * UpgradeMath.DecorMultiplier(upgrades.Decor)
                              * CleanlinessFactor(ref state)
                              * ProgressMath.LevelTrafficFactor(stationLevel)
                              * EventFactor(worldEvent)
                              * (SystemAPI.HasSingleton<StationStyle>() ? StyleMath.TrafficFactor(SystemAPI.GetSingleton<StationStyle>().Scheme) : 1f)
                              * RenovationFactor(ref state)
                              * CompetitionFactor(ref state)
                              * (SystemAPI.HasSingleton<PropEffects>() ? PropMath.TrafficFactor(SystemAPI.GetSingleton<PropEffects>()) : 1f);

            intensity *= VisitorTrafficFactor(ref state);

            var props = SystemAPI.HasSingleton<PropEffects>() ? SystemAPI.GetSingleton<PropEffects>() : default;
            ref var spawner = ref SystemAPI.GetComponentRW<CarSpawner>(spawnerEntity).ValueRW;

            // Regulars and special guests sent by VisitorSystem come first and may exceed the car limit a little.
            if (SystemAPI.HasBuffer<SpawnRequest>(spawnerEntity))
            {
                var requests = SystemAPI.GetBuffer<SpawnRequest>(spawnerEntity);
                if (requests.Length > 0)
                {
                    var request = requests[0];
                    request.Delay -= SystemAPI.Time.DeltaTime;
                    requests[0] = request;
                    if (request.Delay <= 0f && ActiveCars(ref state) < spawner.MaxCars + ExtraCarsForGuests)
                    {
                        requests.RemoveAt(0);
                        SpawnCar(ref state, spawnerEntity, ref spawner, prefabs, request, upgrades, props);
                        return;
                    }
                }
            }

            spawner.Timer -= SystemAPI.Time.DeltaTime * intensity;
            if (spawner.Timer > 0f)
                return;

            spawner.Timer = spawner.BaseInterval * spawner.Random.NextFloat(0.6f, 1.4f);
            if (ActiveCars(ref state) >= spawner.MaxCars)
                return;

            var customer = CustomerProfiles.Pick(spawner.Random.NextFloat(), stationLevel, hour, worldEvent == WorldEventKind.RushHour);
            // Lamps scare thieves off at night: some of them become ordinary customers.
            if (customer == CustomerType.Thief && LightingMath.NightFactor(hour) > 0.5f &&
                spawner.Random.NextFloat() > PropMath.NightCrimeFactor(props.Lamps))
                customer = CustomerType.Regular;
            // A police contract keeps most thieves away.
            if (customer == CustomerType.Thief && PoliceOnDuty(ref state) &&
                spawner.Random.NextFloat() > ContractMath.PoliceThiefShare)
                customer = CustomerType.Regular;

            SpawnCar(ref state, spawnerEntity, ref spawner, prefabs, new SpawnRequest { Customer = customer }, upgrades, props);
        }

        private void SpawnCar(ref SystemState state, Entity spawnerEntity, ref CarSpawner spawner, DynamicBuffer<CarPrefabElement> prefabs,
            in SpawnRequest request, in StationUpgrades upgrades, in PropEffects props)
        {
            var customer = request.Customer;
            var profile = CustomerProfiles.Get(customer);

            var prefab = prefabs[spawner.Random.NextInt(prefabs.Length)].Prefab;
            float scale = SystemAPI.HasComponent<LocalTransform>(prefab)
                ? SystemAPI.GetComponent<LocalTransform>(prefab).Scale
                : 1f;
            scale *= SizeFactor(customer);
            float patience = spawner.Random.NextFloat(spawner.PatienceRange.x, spawner.PatienceRange.y)
                             * profile.PatienceMultiplier
                             * UpgradeMath.PatienceMultiplier(upgrades.Comfort)
                             * PropMath.PatienceFactor(props.Benches);
            float liters = spawner.Random.NextFloat(spawner.LitersRange.x, spawner.LitersRange.y)
                           * (request.HasHabits ? request.LitersMultiplier : profile.LitersMultiplier);
            var fuel = request.HasHabits ? request.Fuel
                : profile.DieselOnly ? FuelType.Diesel
                : StationMath.PickFuelType(spawner.Random.NextFloat());

            bool wantsShop = request.HasHabits ? request.WantsShop : spawner.Random.NextFloat() < profile.ShopChance;
            bool wantsWash = request.HasHabits ? request.WantsWash : spawner.Random.NextFloat() < profile.WashChance;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var car = ecb.Instantiate(prefab);
            ecb.AddComponent(car, LocalTransform.FromPositionRotationScale(spawner.SpawnPosition, spawner.SpawnRotation, scale));
            uint order = spawner.NextArrivalOrder++;
            ecb.AddComponent(car, new Car
            {
                State = CarState.Arriving,
                Customer = customer,
                FuelType = fuel,
                RequestedLiters = liters,
                ReceivedLiters = 0f,
                Pump = Entity.Null,
                // Ambulances and police go to the front of the queue, contract vehicles right after them.
                ArrivalOrder = customer == CustomerType.Emergency ? 0u : request.ContractId > 0 ? 1u : order,
                WantsShop = wantsShop,
                WantsWash = wantsWash,
                NeedsTires = spawner.Random.NextFloat() < profile.TireChance,
                RegularId = request.RegularId,
                Passengers = request.Passengers,
                ContractId = request.ContractId
            });
            ecb.AddComponent(car, new Patience { Current = patience, Max = patience });
            ecb.AddComponent(car, new CarMovement
            {
                Speed = spawner.Random.NextFloat(spawner.SpeedRange.x, spawner.SpeedRange.y) * profile.SpeedMultiplier,
                TurnSpeed = 4f
            });

            var path = ecb.AddBuffer<PathPoint>(car);
            var entryRoute = SystemAPI.GetBuffer<EntryRoutePoint>(spawnerEntity);
            for (int i = 0; i < entryRoute.Length; i++)
                path.Add(new PathPoint { Position = entryRoute[i].Position });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (request.RegularId > 0 && SystemAPI.HasSingleton<StationEvent>())
            {
                bool firstVisit = true;
                if (SystemAPI.HasSingleton<RegularState>())
                {
                    var regulars = SystemAPI.GetSingletonBuffer<RegularState>(true);
                    int index = request.RegularId - 1;
                    firstVisit = index >= regulars.Length || regulars[index].Visits == 0;
                }

                StationEvent.Push(SystemAPI.GetSingletonBuffer<StationEvent>(), StationEventType.RegularArrived, default,
                    firstVisit ? 1f : 0f, request.RegularId);
            }
        }

        private const int ExtraCarsForGuests = 6;

        private bool PoliceOnDuty(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Contract>())
                return false;

            var contracts = SystemAPI.GetSingletonBuffer<Contract>(true);
            for (int i = 0; i < contracts.Length; i++)
            {
                if (contracts[i].Type == ContractType.Police)
                    return true;
            }

            return false;
        }

        /// <summary>Tour buses are big, bikes are small (placeholders until real models).</summary>
        private static float SizeFactor(CustomerType customer) => customer switch
        {
            CustomerType.TourBus => 1.5f,
            CustomerType.TowTruck => 1.25f,
            CustomerType.Biker => 0.7f,
            _ => 1f
        };

        /// <summary>A critic's article and the friends loyal regulars bring along.</summary>
        private float VisitorTrafficFactor(ref SystemState state)
        {
            float buzz = SystemAPI.HasSingleton<Buzz>() ? SystemAPI.GetSingleton<Buzz>().Factor : 1f;
            float factor = buzz > 0f ? buzz : 1f;
            if (!SystemAPI.HasSingleton<RegularState>())
                return factor;

            var regulars = SystemAPI.GetSingletonBuffer<RegularState>(true);
            int loyal = 0;
            for (int i = 0; i < regulars.Length; i++)
            {
                if (!regulars[i].Lost && regulars[i].Loyalty > 0.8f)
                    loyal++;
            }

            return factor * VisitorMath.FriendsTrafficFactor(loyal);
        }

        /// <summary>Cars on the station, not counting guests sleeping at the motel or on the truck parking.</summary>
        private int ActiveCars(ref SystemState state)
        {
            int count = 0;
            foreach (var car in SystemAPI.Query<RefRO<Car>>())
            {
                if (car.ValueRO.State is not (CarState.Parked or CarState.InMotel or CarState.DrivingToParking or CarState.DrivingToMotel))
                    count++;
            }

            return count;
        }

        private float CleanlinessFactor(ref SystemState state) =>
            SystemAPI.HasSingleton<StationCleanliness>()
                ? StationMath.CleanlinessTrafficFactor(SystemAPI.GetSingleton<StationCleanliness>().Value)
                : 1f;

        /// <summary>Drivers split between us and the station across the road.</summary>
        private float CompetitionFactor(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Competitor>())
                return 1f;
            var rival = SystemAPI.GetSingleton<Competitor>();
            return rival.Active && !rival.BoughtOut ? CompetitionMath.TrafficFactor(rival.OurShare) : 1f;
        }

        private float RenovationFactor(ref SystemState state)
        {
            float factor = 1f;
            foreach (var renovation in SystemAPI.Query<RefRO<Renovation>>())
            {
                if (renovation.ValueRO.Done)
                    factor += RenovationMath.TrafficBonus(renovation.ValueRO.Kind);
            }

            return factor;
        }

        private static float EventFactor(WorldEventKind kind) => kind switch
        {
            WorldEventKind.RushHour => 2.5f,
            WorldEventKind.Sandstorm => 0.4f,
            _ => 1f
        };
    }
}
