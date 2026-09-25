using GasStation.Components;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Adds this frame's reviews to the day's average rating.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(AchievementSystem))]
    public partial struct ReviewSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>(true);
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type != StationEventType.CustomerReview)
                    continue;

                economy.DayRatingSum += events[i].Value;
                economy.DayRatingCount++;
            }
        }
    }
}
