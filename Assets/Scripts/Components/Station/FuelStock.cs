using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>Station tank for one fuel type. Buffer index equals (int)FuelType.</summary>
    [InternalBufferCapacity(FuelTypes.Count)]
    public struct FuelStock : IBufferElementData
    {
        public float Amount;
        public float Capacity;
        public float BuyPrice;
        public float SellPrice;
        public float MarketPrice;
        /// <summary>Long-term average market price; daily prices wander around it.</summary>
        public float BaseMarketPrice;
    }

    public struct FuelDelivery : IComponentData
    {
        public FuelType Type;
        public float Liters;
        public float TimeLeft;
        /// <summary>The truck bringing it. While it drives and unloads, the timer does not run.</summary>
        public Entity Truck;
    }
}
