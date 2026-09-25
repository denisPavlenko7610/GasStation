using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Waiting customers drop litter around their cars.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(CarArrivalSystem))]
    public partial struct LitterSystem : ISystem
    {
        private EntityQuery _trash;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrashSpawner>();
            _trash = SystemAPI.QueryBuilder().WithAll<Trash>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnerEntity = SystemAPI.GetSingletonEntity<TrashSpawner>();
            var prefabs = SystemAPI.GetBuffer<TrashPrefabElement>(spawnerEntity);
            if (prefabs.Length == 0)
                return;

            ref var spawner = ref SystemAPI.GetComponentRW<TrashSpawner>(spawnerEntity).ValueRW;
            int trashCount = _trash.CalculateEntityCount();
            float chance = spawner.LitterChancePerSecond * SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var bins = new NativeList<float2>(Allocator.Temp);
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
            {
                if (prop.ValueRO.Type == PropType.TrashBin)
                    bins.Add(prop.ValueRO.Position.xz);
            }

            float binRadius = PropMath.Get(PropType.TrashBin).Radius;

            foreach (var (car, transform) in SystemAPI.Query<RefRO<Car>, RefRO<LocalTransform>>())
            {
                var carState = car.ValueRO.State;
                bool waiting = carState is CarState.Queued or CarState.WaitingForService or CarState.Fueling;
                float carChance = chance * CustomerProfiles.Get(car.ValueRO.Customer).LitterMultiplier;
                if (!waiting || trashCount >= spawner.MaxTrash || spawner.Random.NextFloat() >= carChance)
                    continue;

                var prefab = prefabs[spawner.Random.NextInt(prefabs.Length)].Prefab;
                float scale = SystemAPI.HasComponent<LocalTransform>(prefab)
                    ? SystemAPI.GetComponent<LocalTransform>(prefab).Scale
                    : 1f;

                float angle = spawner.Random.NextFloat(0f, 2f * math.PI);
                float distance = spawner.Random.NextFloat(1.5f, 3f);
                float3 position = transform.ValueRO.Position + new float3(math.cos(angle), 0f, math.sin(angle)) * distance;
                position.y = transform.ValueRO.Position.y;

                // A trash bin nearby: most customers use it instead of the ground.
                if (NearAny(bins, position.xz, binRadius) &&
                    spawner.Random.NextFloat() >= PropMath.LitterFactor(true))
                    continue;

                var trash = ecb.Instantiate(prefab);
                ecb.AddComponent(trash, LocalTransform.FromPositionRotationScale(
                    position, quaternion.RotateY(spawner.Random.NextFloat(0f, 2f * math.PI)), scale));
                ecb.AddComponent(trash, new Trash());
                trashCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            bins.Dispose();
        }

        private static bool NearAny(NativeList<float2> points, float2 position, float radius)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (math.distancesq(points[i], position) <= radius * radius)
                    return true;
            }

            return false;
        }
    }
}
