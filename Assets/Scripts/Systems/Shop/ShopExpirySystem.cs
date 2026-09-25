using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Every night the stock gets a day older; half of an expired perishable shelf is written off.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial struct ShopExpirySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Shop>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            bool newDay = StationEvent.Contains(events, StationEventType.DayEnded);
            if (!newDay)
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            for (int i = 0; i < shelves.Length && i < ProductTypes.Count; i++)
            {
                var shelf = shelves[i];
                var type = (ProductType)i;
                if (shelf.Stock <= 0)
                {
                    shelf.Age = 0f;
                    shelves[i] = shelf;
                    continue;
                }

                shelf.Age += 1f;
                int spoiled = ShopMath.Spoiled(type, shelf.Stock, shelf.Age);
                if (spoiled > 0)
                {
                    shelf.Stock -= spoiled;
                    // What is left is the newer part of the stock.
                    shelf.Age = ShopMath.ShelfLife(type) - 1f;
                    StationEvent.Push(events, StationEventType.ProductsSpoiled, default, spoiled, i);
                }

                shelves[i] = shelf;
            }
        }
    }
}
