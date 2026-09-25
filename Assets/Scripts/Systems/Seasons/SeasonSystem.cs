using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Every midnight: the season from the calendar (two weeks each) and a new roll for the weather.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial struct SeasonSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SeasonState>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            bool newDay = false;
            for (int i = 0; i < events.Length; i++)
                newDay |= events[i].Type == StationEventType.DayEnded;
            if (!newDay)
                return;

            ref var season = ref SystemAPI.GetSingletonRW<SeasonState>().ValueRW;
            var next = SeasonMath.SeasonOf(SystemAPI.GetSingleton<GameTime>().Day);
            if (next != season.Season)
            {
                season.Season = next;
                StationEvent.Push(events, StationEventType.SeasonChanged, default, (float)next);
            }

            var weather = SeasonMath.RollWeather(season.Season, season.Random.NextFloat());
            if (weather != season.Weather && weather != WeatherKind.Clear)
                StationEvent.Push(events, StationEventType.WeatherChanged, default, (float)weather);
            season.Weather = weather;
        }
    }

    /// <summary>Adds the plates of paying customers to the collection.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(QuestSystem))]
    public partial struct PlateCollectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlateCollection>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var collection = ref SystemAPI.GetSingletonRW<PlateCollection>().ValueRW;
            int count = events.Length;
            for (int i = 0; i < count; i++)
            {
                if (events[i].Type != StationEventType.PlateSeen)
                    continue;

                int plate = events[i].Subject;
                if (plate < 0 || plate >= PlateMath.Count || PlateMath.Has(collection.Seen, plate))
                    continue;

                collection.Seen |= 1 << plate;
                StationEvent.Push(events, StationEventType.NewPlate, default, PlateMath.Collected(collection.Seen), plate);
            }
        }
    }
}
