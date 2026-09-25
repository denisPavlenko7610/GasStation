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
        private EntityQuery _cars;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<FuelStock>();
            state.RequireForUpdate<StationUpgrades>();
            _cars = SystemAPI.QueryBuilder().WithAll<Car>().Build();
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
                              * RenovationFactor(ref state);

            ref var spawner = ref SystemAPI.GetComponentRW<CarSpawner>(spawnerEntity).ValueRW;
            spawner.Timer -= SystemAPI.Time.DeltaTime * intensity;
            if (spawner.Timer > 0f)
                return;

            spawner.Timer = spawner.BaseInterval * spawner.Random.NextFloat(0.6f, 1.4f);
            if (_cars.CalculateEntityCount() >= spawner.MaxCars)
                return;

            var customer = CustomerProfiles.Pick(spawner.Random.NextFloat(), stationLevel, hour, worldEvent == WorldEventKind.RushHour);
            var profile = CustomerProfiles.Get(customer);

            var prefab = prefabs[spawner.Random.NextInt(prefabs.Length)].Prefab;
            float scale = SystemAPI.HasComponent<LocalTransform>(prefab)
                ? SystemAPI.GetComponent<LocalTransform>(prefab).Scale
                : 1f;
            float patience = spawner.Random.NextFloat(spawner.PatienceRange.x, spawner.PatienceRange.y)
                             * profile.PatienceMultiplier
                             * UpgradeMath.PatienceMultiplier(upgrades.Comfort);
            float liters = spawner.Random.NextFloat(spawner.LitersRange.x, spawner.LitersRange.y) * profile.LitersMultiplier;
            var fuel = profile.DieselOnly ? FuelType.Diesel : StationMath.PickFuelType(spawner.Random.NextFloat());

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var car = ecb.Instantiate(prefab);
            ecb.AddComponent(car, LocalTransform.FromPositionRotationScale(spawner.SpawnPosition, spawner.SpawnRotation, scale));
            ecb.AddComponent(car, new Car
            {
                State = CarState.Arriving,
                Customer = customer,
                FuelType = fuel,
                RequestedLiters = liters,
                ReceivedLiters = 0f,
                Pump = Entity.Null,
                ArrivalOrder = spawner.NextArrivalOrder++,
                WantsShop = spawner.Random.NextFloat() < profile.ShopChance,
                WantsWash = spawner.Random.NextFloat() < profile.WashChance,
                NeedsTires = spawner.Random.NextFloat() < profile.TireChance
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
        }

        private float CleanlinessFactor(ref SystemState state) =>
            SystemAPI.HasSingleton<StationCleanliness>()
                ? StationMath.CleanlinessTrafficFactor(SystemAPI.GetSingleton<StationCleanliness>().Value)
                : 1f;

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
