using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Advances the clock and closes the books at midnight.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    public partial struct GameTimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingletonRW<GameTime>();
            float deltaHours = SystemAPI.Time.DeltaTime * time.ValueRO.MinutesPerSecond / 60f;
            if (!StationMath.AdvanceClock(ref time.ValueRW.Hour, ref time.ValueRW.Day, deltaHours))
                return;

            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            economy.Money -= economy.DailyFixedCosts;
            economy.DayExpenses += economy.DailyFixedCosts;

            SystemAPI.SetSingleton(new DayReport
            {
                Day = time.ValueRO.Day - 1,
                Income = economy.DayIncome,
                Expenses = economy.DayExpenses,
                Served = economy.DayServed,
                Lost = economy.DayLost
            });

            StationEvent.Push(SystemAPI.GetSingletonBuffer<StationEvent>(), StationEventType.DayEnded, default, economy.DayIncome - economy.DayExpenses);

            economy.DayIncome = 0f;
            economy.DayExpenses = 0f;
            economy.DayServed = 0;
            economy.DayLost = 0;
        }
    }
}
