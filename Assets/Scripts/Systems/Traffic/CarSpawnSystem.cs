using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Spawns customers. Traffic depends on time of day, reputation and fuel prices.</summary>
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

        private float CleanlinessFactor(ref SystemState state) =>
            SystemAPI.HasSingleton<StationCleanliness>()
                ? StationMath.CleanlinessTrafficFactor(SystemAPI.GetSingleton<StationCleanliness>().Value)
                : 1f;

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
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);

            float attractiveness = 0f;
            for (int i = 0; i < stock.Length; i++)
                attractiveness += StationMath.PriceAttractiveness(stock[i].SellPrice, stock[i].MarketPrice);
            attractiveness /= stock.Length;

            float intensity = StationMath.TrafficIntensity(hour)
                              * StationMath.ReputationFactor(reputation)
                              * attractiveness
                              * UpgradeMath.TrafficMultiplier(upgrades.Advertising)
                              * CleanlinessFactor(ref state);

            ref var spawner = ref SystemAPI.GetComponentRW<CarSpawner>(spawnerEntity).ValueRW;
            spawner.Timer -= SystemAPI.Time.DeltaTime * intensity;
            if (spawner.Timer > 0f)
                return;

            spawner.Timer = spawner.BaseInterval * spawner.Random.NextFloat(0.6f, 1.4f);
            if (_cars.CalculateEntityCount() >= spawner.MaxCars)
                return;

            var prefab = prefabs[spawner.Random.NextInt(prefabs.Length)].Prefab;
            float scale = SystemAPI.HasComponent<LocalTransform>(prefab)
                ? SystemAPI.GetComponent<LocalTransform>(prefab).Scale
                : 1f;
            float patience = spawner.Random.NextFloat(spawner.PatienceRange.x, spawner.PatienceRange.y)
                             * UpgradeMath.PatienceMultiplier(upgrades.Comfort);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var car = ecb.Instantiate(prefab);
            ecb.AddComponent(car, LocalTransform.FromPositionRotationScale(spawner.SpawnPosition, spawner.SpawnRotation, scale));
            ecb.AddComponent(car, new Car
            {
                State = CarState.Arriving,
                FuelType = StationMath.PickFuelType(spawner.Random.NextFloat()),
                RequestedLiters = spawner.Random.NextFloat(spawner.LitersRange.x, spawner.LitersRange.y),
                ReceivedLiters = 0f,
                Pump = Entity.Null,
                ArrivalOrder = spawner.NextArrivalOrder++
            });
            ecb.AddComponent(car, new Patience { Current = patience, Max = patience });
            ecb.AddComponent(car, new CarMovement
            {
                Speed = spawner.Random.NextFloat(spawner.SpeedRange.x, spawner.SpeedRange.y),
                TurnSpeed = 4f
            });

            var path = ecb.AddBuffer<PathPoint>(car);
            var entryRoute = SystemAPI.GetBuffer<EntryRoutePoint>(spawnerEntity);
            for (int i = 0; i < entryRoute.Length; i++)
                path.Add(new PathPoint { Position = entryRoute[i].Position });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
