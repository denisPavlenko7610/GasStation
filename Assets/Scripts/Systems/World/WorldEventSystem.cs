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
    /// Once per game hour rolls for a random event: night vandals, a sanitary inspection, a rush of tourists
    /// or a sandstorm. Timed events run for a few game hours.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(MarketSystem))]
    public partial struct WorldEventSystem : ISystem
    {
        private const int VandalTrash = 8;
        private const float SandstormLitterPerSecond = 0.3f;
        private const float LotHalfWidth = 25f;
        private const float LotHalfDepth = 18f;

        private EntityQuery _trash;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldEvents>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
            _trash = SystemAPI.QueryBuilder().WithAll<Trash>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<GameTime>();
            float deltaHours = SystemAPI.Time.DeltaTime * time.MinutesPerSecond / 60f;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            ref var world = ref SystemAPI.GetSingletonRW<WorldEvents>().ValueRW;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            if (world.Active != WorldEventKind.None)
            {
                world.HoursLeft -= deltaHours;
                if (world.HoursLeft <= 0f)
                    world.Active = WorldEventKind.None;
            }

            if (world.Active == WorldEventKind.Sandstorm &&
                world.Random.NextFloat() < SandstormLitterPerSecond * SystemAPI.Time.DeltaTime)
            {
                SpawnLitter(ref state, ref world, ecb, 1);
            }

            int hour = (int)time.Hour;
            if (hour != world.LastRolledHour)
            {
                world.LastRolledHour = hour;
                RollEvent(ref state, ref world, ecb, events, hour);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void RollEvent(ref SystemState state, ref WorldEvents world, EntityCommandBuffer ecb,
            DynamicBuffer<StationEvent> events, int hour)
        {
            float roll = world.Random.NextFloat();

            if (hour < 4)
            {
                if (roll < 0.2f)
                {
                    SpawnLitter(ref state, ref world, ecb, VandalTrash);
                    StationEvent.Push(events, StationEventType.Vandals, default, VandalTrash);
                }

                return;
            }

            if (hour >= 9 && hour <= 17 && roll < 0.05f)
            {
                Inspect(ref state, events);
                return;
            }

            if (world.Active != WorldEventKind.None)
                return;

            if (hour >= 7 && hour <= 19 && roll < 0.11f)
            {
                world.Active = WorldEventKind.RushHour;
                world.HoursLeft = 1.5f;
                StationEvent.Push(events, StationEventType.RushHourStarted);
            }
            else if (hour >= 10 && hour <= 18 && roll < 0.14f)
            {
                world.Active = WorldEventKind.Sandstorm;
                world.HoursLeft = 2f;
                StationEvent.Push(events, StationEventType.SandstormStarted);
            }
        }

        private void Inspect(ref SystemState state, DynamicBuffer<StationEvent> events)
        {
            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>()
                ? SystemAPI.GetSingleton<StationCleanliness>().Value
                : 1f;
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;

            if (cleanliness >= ProgressMath.InspectionCleanlinessRequired)
            {
                economy.Money += ProgressMath.InspectionReward;
                economy.DayIncome += ProgressMath.InspectionReward;
                economy.Reputation = StationMath.ClampReputation(economy.Reputation + 0.03f);
                StationEvent.Push(events, StationEventType.InspectionPassed, default, ProgressMath.InspectionReward);
            }
            else
            {
                economy.Money -= ProgressMath.InspectionFine;
                economy.DayExpenses += ProgressMath.InspectionFine;
                economy.Reputation = StationMath.ClampReputation(economy.Reputation - 0.05f);
                StationEvent.Push(events, StationEventType.InspectionFailed, default, ProgressMath.InspectionFine);
            }
        }

        /// <summary>Drops litter at random places on the lot, centered on the pumps.</summary>
        private void SpawnLitter(ref SystemState state, ref WorldEvents world, EntityCommandBuffer ecb, int count)
        {
            if (!SystemAPI.HasSingleton<TrashSpawner>())
                return;

            var spawnerEntity = SystemAPI.GetSingletonEntity<TrashSpawner>();
            var spawner = SystemAPI.GetComponent<TrashSpawner>(spawnerEntity);
            var prefabs = SystemAPI.GetBuffer<TrashPrefabElement>(spawnerEntity);
            if (prefabs.Length == 0)
                return;

            count = math.min(count, spawner.MaxTrash - _trash.CalculateEntityCount());

            float3 center = float3.zero;
            int pumps = 0;
            foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
            {
                center += pump.ValueRO.InteractionPoint;
                pumps++;
            }

            if (pumps > 0)
                center /= pumps;

            for (int i = 0; i < count; i++)
            {
                var prefab = prefabs[world.Random.NextInt(prefabs.Length)].Prefab;
                float scale = SystemAPI.HasComponent<LocalTransform>(prefab)
                    ? SystemAPI.GetComponent<LocalTransform>(prefab).Scale
                    : 1f;
                var halfSize = new float2(LotHalfWidth, LotHalfDepth);
                float2 offset = world.Random.NextFloat2(-halfSize, halfSize);
                TrashSpawning.Spawn(ecb, prefab, scale, center + new float3(offset.x, 0f, offset.y),
                    world.Random.NextFloat(0f, 2f * math.PI));
            }
        }
    }
}
