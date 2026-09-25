using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    /// <summary>EV charging bay, opened by the EvCharger upgrade (two chargers per level).</summary>
    public struct ChargingStation : IComponentData
    {
        /// <summary>Price the driver pays per kWh.</summary>
        public float PricePerKwh;
        public Random Random;
    }

    [InternalBufferCapacity(4)]
    public struct ChargerSpot : IBufferElementData
    {
        public float3 Position;
        public quaternion Rotation;
        public Entity Occupant;
    }

    /// <summary>What an electric car is doing at its charger.</summary>
    public struct EvCharge : IComponentData
    {
        public int Spot;
        /// <summary>Seconds of charging left.</summary>
        public float TimeLeft;
        public float Kwh;
        /// <summary>The driver already went to the shop once.</summary>
        public bool Shopped;
    }

    public enum DinerDish : byte
    {
        HotDog = 0,
        Burger = 1
    }

    public static class DinerDishes
    {
        public const int Count = 2;
    }

    /// <summary>The diner next to the shop, opened by the Diner upgrade. Needs ingredients and a cook (or the player).</summary>
    public struct Diner : IComponentData
    {
        /// <summary>Where the cook stands; the player cooks here with E.</summary>
        public float3 Grill;
        public int Ingredients;
        /// <summary>Seconds of cooking done on the current dish.</summary>
        public float CookProgress;
        /// <summary>"Out of ingredients" was already reported; cleared when ingredients arrive.</summary>
        public bool WarnedEmpty;
        public Random Random;
    }

    /// <summary>Ready food on the counter. Buffer index equals (int)DinerDish.</summary>
    [InternalBufferCapacity(DinerDishes.Count)]
    public struct DinerCounter : IBufferElementData
    {
        public int Ready;
        public float Price;
        /// <summary>Game hours before the food on the counter goes cold and is thrown away.</summary>
        public float HoursLeft;
    }
}
