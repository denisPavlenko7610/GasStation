using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>At the start of every day the fuel market moves: new market and wholesale prices.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial struct MarketSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldEvents>();
            state.RequireForUpdate<FuelStock>();
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

            ref var world = ref SystemAPI.GetSingletonRW<WorldEvents>().ValueRW;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>();
            // An oil crisis pulls prices up towards a higher base.
            float crisis = SystemAPI.HasSingleton<RoadEvent>() ? RoadMath.MarketFactor(SystemAPI.GetSingleton<RoadEvent>().Kind) : 1f;
            float totalChange = 0f;

            for (int i = 0; i < stock.Length; i++)
            {
                var entry = stock[i];
                float basePrice = (entry.BaseMarketPrice > 0f ? entry.BaseMarketPrice : entry.MarketPrice) * crisis;
                float next = ProgressMath.NextMarketPrice(entry.MarketPrice, basePrice, world.Random.NextFloat());
                totalChange += entry.MarketPrice > 0f ? next / entry.MarketPrice - 1f : 0f;
                entry.MarketPrice = next;
                entry.BuyPrice = ProgressMath.BuyPrice(next);
                stock[i] = entry;
            }

            StationEvent.Push(events, StationEventType.MarketChanged, default, totalChange / stock.Length * 100f);
        }
    }
}
