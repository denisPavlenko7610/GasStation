using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Remembers how regulars were treated (loyalty, grudges, leaving for the competitor) and turns the
    /// critic's visit into an article that changes traffic for a week.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(QuestSystem))]
    public partial struct VisitOutcomeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationEvent>();
            state.RequireForUpdate<GameTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            int day = SystemAPI.GetSingleton<GameTime>().Day;
            bool hasRegulars = SystemAPI.HasSingleton<RegularState>();
            bool hasBuzz = SystemAPI.HasSingleton<Buzz>();

            int count = events.Length;
            for (int i = 0; i < count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case StationEventType.RegularVisit when hasRegulars:
                        RememberVisit(SystemAPI.GetSingletonBuffer<RegularState>(), events, e.Subject, e.Value, day);
                        break;

                    case StationEventType.CriticVisit when hasBuzz:
                    {
                        float factor = VisitorMath.ArticleFactor(e.Value);
                        if (factor == 1f)
                            break;
                        SystemAPI.SetSingleton(new Buzz { Factor = factor, DaysLeft = VisitorMath.ArticleDays });
                        StationEvent.Push(events, StationEventType.CriticArticle, default, factor);
                        break;
                    }

                    case StationEventType.DayEnded when hasBuzz:
                    {
                        var buzz = SystemAPI.GetSingleton<Buzz>();
                        if (buzz.DaysLeft > 0 && --buzz.DaysLeft == 0)
                            buzz.Factor = 1f;
                        SystemAPI.SetSingleton(buzz);
                        break;
                    }
                }
            }
        }

        private static void RememberVisit(DynamicBuffer<RegularState> regulars, DynamicBuffer<StationEvent> events, int id,
            float stars, int day)
        {
            int index = id - 1;
            if (index < 0 || index >= regulars.Length)
                return;

            var regular = regulars[index];
            bool wasBestFriend = regular.Loyalty >= VisitorMath.BestFriendLoyalty;
            regular.Visits++;
            regular.LastVisitDay = day;
            regular.LastStars = stars;
            regular.Loyalty = VisitorMath.NextLoyalty(regular.Loyalty, stars);
            regular.Upsets = VisitorMath.IsUpset(stars) ? regular.Upsets + 1 : 0;

            if (regular.Upsets >= VisitorMath.UpsetsToLeave && !regular.Lost)
            {
                regular.Lost = true;
                StationEvent.Push(events, StationEventType.RegularLost, default, 0f, id);
            }
            else if (!wasBestFriend && regular.Loyalty >= VisitorMath.BestFriendLoyalty)
            {
                StationEvent.Push(events, StationEventType.RegularBestFriend, default, 0f, id);
            }

            regulars[index] = regular;
        }
    }
}
