using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Accumulates lifetime statistics from this frame's events and unlocks achievements.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(StationLevelSystem))]
    public partial struct AchievementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationStats>();
            state.RequireForUpdate<Achievements>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var stationEntity = SystemAPI.GetSingletonEntity<StationStats>();
            ref var stats = ref SystemAPI.GetComponentRW<StationStats>(stationEntity).ValueRW;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            int count = events.Length;
            for (int i = 0; i < count; i++)
            {
                var stationEvent = events[i];
                AchievementCatalog.Count(ref stats, stationEvent.Type, stationEvent.Value);

                if (stationEvent.Type != StationEventType.DayEnded)
                    continue;

                stats.DaysPlayed++;
                if (SystemAPI.HasSingleton<DayReport>() && AchievementCatalog.IsPerfectDay(SystemAPI.GetSingleton<DayReport>()))
                    stats.PerfectDays++;
            }

            var context = new AchievementContext
            {
                Stats = stats,
                Level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1,
                Money = SystemAPI.GetSingleton<Economy>().Money,
                Cleanliness = SystemAPI.HasSingleton<StationCleanliness>() ? SystemAPI.GetSingleton<StationCleanliness>().Value : 0f,
                Headcount = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>().Headcount : 0
            };

            ref var achievements = ref SystemAPI.GetSingletonRW<Achievements>().ValueRW;
            for (int i = 0; i < AchievementCatalog.Count; i++)
            {
                if (achievements.Has(i) || !AchievementCatalog.IsReached((AchievementId)i, context))
                    continue;

                achievements.Unlocked |= 1UL << i;
                StationEvent.Push(events, StationEventType.AchievementUnlocked, default, i);
            }
        }
    }
}
