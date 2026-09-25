using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public enum CarState : byte
    {
        Arriving,
        Queued,
        DrivingToPump,
        WaitingForService,
        Fueling,
        Leaving
    }

    public struct Car : IComponentData
    {
        public CarState State;
        public FuelType FuelType;
        public float RequestedLiters;
        public float ReceivedLiters;
        public Entity Pump;
        public uint ArrivalOrder;
    }

    /// <summary>Seconds the customer is willing to wait in the queue and at the pump.</summary>
    public struct Patience : IComponentData
    {
        public float Current;
        public float Max;
    }

    public struct CarMovement : IComponentData
    {
        public float Speed;
        public float TurnSpeed;
    }

    /// <summary>Remaining points the car drives through, in order.</summary>
    [InternalBufferCapacity(4)]
    public struct PathPoint : IBufferElementData
    {
        public float3 Position;
    }
}
