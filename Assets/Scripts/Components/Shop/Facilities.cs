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

    /// <summary>Tire service bay, available after the TireService upgrade. The player (or a mechanic) does the work.</summary>
    public struct TireService : IComponentData
    {
        public float3 Bay;
        public quaternion BayRotation;
        public float Duration;
        public float Price;
        public Entity Occupant;
    }

    public struct TireEntryPoint : IBufferElementData
    {
        public float3 Position;
    }

    /// <summary>Paint scheme of the station. 0 = peeling paint of the abandoned station.</summary>
    public struct StationStyle : IComponentData
    {
        public int Scheme;
    }

    /// <summary>Timer of the SupplyManager upgrade.</summary>
    public struct SupplyManagerState : IComponentData
    {
        public float Timer;
    }
}
