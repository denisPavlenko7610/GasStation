using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public enum CustomerType : byte
    {
        Regular,
        Trucker,
        Hurry,
        Tourist,
        Thief
    }

    public enum CarState : byte
    {
        Arriving,
        Queued,
        DrivingToPump,
        WaitingForService,
        Fueling,
        Leaving,
        /// <summary>Fueled and paid; the driver walked to the shop, the car still occupies the pump.</summary>
        Shopping,
        DrivingToWash,
        Washing,
        /// <summary>Done at the pump (and the shop); CarWashSystem frees the pump and picks the way out.</summary>
        ReadyToLeave
    }

    public struct Car : IComponentData
    {
        public CarState State;
        public CustomerType Customer;
        public FuelType FuelType;
        public float RequestedLiters;
        public float ReceivedLiters;
        public Entity Pump;
        public uint ArrivalOrder;
        /// <summary>Seconds spent at the pump waiting for service.</summary>
        public float ServiceWait;
        public bool WantsShop;
        public bool WantsWash;
        /// <summary>The driver is out of the car (a pedestrian walking to or from the shop).</summary>
        public bool DriverAway;
        /// <summary>Generic countdown: shopping without a pedestrian, washing.</summary>
        public float Timer;
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
