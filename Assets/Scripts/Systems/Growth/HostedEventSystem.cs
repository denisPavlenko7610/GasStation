using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Events the player plans on the laptop for the next day. During the event hours a crowd comes
    /// (CarSpawnSystem counts them); at the end every visitor pays a ticket and the station's preparation
    /// decides the reputation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial struct HostedEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HostedEvents>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<GameTime>();
            ref var hosted = ref SystemAPI.GetSingletonRW<HostedEvents>().ValueRW;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            if (hosted.Active != HostedEventKind.None)
            {
                if (!HostedEventMath.IsOn(hosted.Active, time.Hour))
                    Finish(ref state, ref hosted, events, time.Day);
                return;
            }

            if (hosted.Planned == HostedEventKind.None)
                return;

            // Plans for a day that is already over are dropped (the money is gone).
            if (time.Day > hosted.PlannedDay)
            {
                hosted.Planned = HostedEventKind.None;
                return;
            }

            if (time.Day == hosted.PlannedDay && HostedEventMath.IsOn(hosted.Planned, time.Hour))
            {
                hosted.Active = hosted.Planned;
                hosted.Planned = HostedEventKind.None;
                hosted.Attendees = 0;
                StationEvent.Push(events, StationEventType.HostedEventStarted, default, (float)hosted.Active);
            }
        }

        private void Finish(ref SystemState state, ref HostedEvents hosted, DynamicBuffer<StationEvent> events, int day)
        {
            var info = HostedEventMath.Get(hosted.Active);
            float income = hosted.Attendees * info.Ticket;
            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>() ? SystemAPI.GetSingleton<StationCleanliness>().Value : 1f;
            float preparation = HostedEventMath.Preparation(cleanliness, ShelvesFull(ref state), StaffOnShift(ref state));

            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            economy.Money += income;
            economy.DayIncome += income;
            economy.Reputation = StationMath.ClampReputation(economy.Reputation + HostedEventMath.ReputationDelta(preparation));

            StationEvent.Push(events, StationEventType.HostedEventEnded, default, income, (int)(preparation * 100f));
            hosted.LastHostedDay = day;
            hosted.Active = HostedEventKind.None;
            hosted.Attendees = 0;
        }

        private float ShelvesFull(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return 1f;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            float stock = 0f, capacity = 0f;
            for (int i = 0; i < shelves.Length; i++)
            {
                stock += shelves[i].Stock;
                capacity += shelves[i].Capacity;
            }

            return capacity > 0f ? stock / capacity : 1f;
        }

        private bool StaffOnShift(ref SystemState state) =>
            SystemAPI.HasSingleton<StaffPower>() && SystemAPI.GetSingleton<StaffPower>().Headcount > 0 &&
            (SystemAPI.GetSingleton<StaffPower>().Attendant + SystemAPI.GetSingleton<StaffPower>().Cashier +
             SystemAPI.GetSingleton<StaffPower>().Janitor) > 0f;
    }
}
