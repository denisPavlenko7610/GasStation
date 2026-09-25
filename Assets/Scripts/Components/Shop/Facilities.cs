using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    /// <summary>Overnight truck parking. Spots open two at a time with the TruckParking upgrade.</summary>
    public struct TruckParking : IComponentData
    {
        public float FeePerNight;
        public float3 Entry;
        public Random Random;
    }

    [InternalBufferCapacity(6)]
    public struct ParkingSpot : IBufferElementData
    {
        public float3 Position;
        public quaternion Rotation;
        public Entity Occupant;
    }

    /// <summary>Restroom next to the shop. Gets dirty with every visit; the player or janitors clean it.</summary>
    public struct Restroom : IComponentData
    {
        public float3 Door;
        /// <summary>0 = clean, 1 = disgusting.</summary>
        public float Dirt;
        public float DirtPerVisit;
        public float VisitChance;
    }

    /// <summary>Timer of the SupplyManager upgrade.</summary>
    public struct SupplyManagerState : IComponentData
    {
        public float Timer;
    }
}
