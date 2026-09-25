using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Life on the highway: sometimes an accident closes the road for two hours (then everybody arrives at
    /// once); now and then a multi-day event starts at midnight — road works, a festival or an oil crisis.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    [UpdateBefore(typeof(MarketSystem))]
    public partial struct RoadEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RoadEvent>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<GameTime>();
            float deltaHours = SystemAPI.Time.DeltaTime * time.MinutesPerSecond / 60f;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var road = ref SystemAPI.GetSingletonRW<RoadEvent>().ValueRW;

            if (road.Kind != RoadEventKind.None)
            {
                road.HoursLeft -= deltaHours;
                if (road.HoursLeft <= 0f)
                {
                    if (road.Kind == RoadEventKind.Accident)
                    {
                        Start(ref road, events, RoadEventKind.AfterAccident);
                    }
                    else
                    {
                        StationEvent.Push(events, StationEventType.RoadEventEnded, default, (float)road.Kind);
                        road.Kind = RoadEventKind.None;
                    }
                }
            }

            bool newDay = false;
            for (int i = 0; i < events.Length; i++)
                newDay |= events[i].Type == StationEventType.DayEnded;
            if (newDay && road.Kind == RoadEventKind.None && road.Random.NextFloat() < RoadMath.DailyChance)
                Start(ref road, events, RoadMath.PickDaily(road.Random.NextFloat()));

            int hour = (int)time.Hour;
            if (hour == road.LastRolledHour)
                return;

            road.LastRolledHour = hour;
            if (road.Kind == RoadEventKind.None && hour >= 7 && hour <= 20 && road.Random.NextFloat() < RoadMath.AccidentChancePerHour)
                Start(ref road, events, RoadEventKind.Accident);
        }

        private static void Start(ref RoadEvent road, DynamicBuffer<StationEvent> events, RoadEventKind kind)
        {
            road.Kind = kind;
            road.HoursLeft = RoadMath.Hours(kind);
            StationEvent.Push(events, StationEventType.RoadEventStarted, default, (float)kind);
        }
    }
}
