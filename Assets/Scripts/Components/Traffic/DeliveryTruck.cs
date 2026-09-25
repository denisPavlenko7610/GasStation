using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    /// <summary>Prefabs and unloading places of the fuel tanker and the goods truck.</summary>
    public struct DeliveryTrucks : IComponentData
    {
        public Entity FuelTruckPrefab;
        public Entity CargoTruckPrefab;
        public float3 FuelUnload;
        public quaternion FuelUnloadRotation;
        public float3 CargoUnload;
        public quaternion CargoUnloadRotation;
        public float Speed;
        public float UnloadTime;
        /// <summary>A truck sets off when the delivery has this many seconds left.</summary>
        public float LeadTime;
    }

    public enum TruckState : byte
    {
        Arriving,
        Unloading,
        Leaving
    }

    public struct DeliveryTruck : IComponentData
    {
        public Entity Delivery;
        public bool Fuel;
        public TruckState State;
        public float Timer;
    }
}
