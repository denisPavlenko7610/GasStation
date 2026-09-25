using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Every night the station is rated 0–5 stars: rating of the last week, cleanliness, level and services.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial struct StarSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationStars>();
            state.RequireForUpdate<StationEvent>();
            state.RequireForUpdate<StationUpgrades>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            bool newDay = StationEvent.Contains(events, StationEventType.DayEnded);
            if (!newDay)
                return;

            float rating = 0f;
            int days = 0;
            if (SystemAPI.HasSingleton<DayHistoryEntry>())
            {
                var history = SystemAPI.GetSingletonBuffer<DayHistoryEntry>(true);
                for (int i = history.Length - 1; i >= 0 && i >= history.Length - StarMath.RatingDays; i--)
                {
                    if (history[i].Rating <= 0f)
                        continue;
                    rating += history[i].Rating;
                    days++;
                }
            }

            rating = days > 0 ? rating / days : 0f;
            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>() ? SystemAPI.GetSingleton<StationCleanliness>().Value : 1f;
            int level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            int services = StarMath.Services(SystemAPI.GetSingleton<StationUpgrades>());

            ref var stars = ref SystemAPI.GetSingletonRW<StationStars>().ValueRW;
            int earned = StarMath.Evaluate(rating, cleanliness, level, services);
            if (earned > stars.Stars)
                StationEvent.Push(events, StationEventType.StarGained, default, earned);
            else if (earned < stars.Stars)
                StationEvent.Push(events, StationEventType.StarLost, default, earned);

            stars.Stars = earned;
            if (earned > stars.Best)
                stars.Best = earned;
        }
    }
}
