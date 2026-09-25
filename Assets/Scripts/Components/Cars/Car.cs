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
        Thief,
        // Special guests: never picked at random, only sent by VisitorSystem.
        /// <summary>Incognito food-and-fuel critic; a great or awful visit makes the news.</summary>
        Critic,
        Biker,
        /// <summary>Ambulance or police: goes to the front of the queue.</summary>
        Emergency,
        /// <summary>Brings a crowd of passengers into the shop.</summary>
        TourBus,
        /// <summary>Brings a broken-down car: always needs the tire service, pays double.</summary>
        TowTruck,
        /// <summary>Electric car: skips the pumps, charges at a charger while the driver shops and eats.</summary>
        Electric
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
        /// <summary>Done at the pump (and the shop); ParkingSystem / CarWashSystem free the pump and pick the way out.</summary>
        ReadyToLeave,
        DrivingToParking,
        /// <summary>A trucker sleeping on the parking lot until morning.</summary>
        Parked,
        DrivingToTires,
        WaitingForTires,
        ChangingTires,
        DrivingToMotel,
        /// <summary>Guests sleeping in a motel room until morning; the car stands at the room.</summary>
        InMotel,
        DrivingToCharger,
        /// <summary>At a charger; the driver may be away in the shop or the diner.</summary>
        Charging
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
        public bool NeedsTires;
        /// <summary>The driver is out of the car (a pedestrian walking to or from the shop).</summary>
        public bool DriverAway;
        /// <summary>Generic countdown: shopping without a pedestrian, washing.</summary>
        public float Timer;
        public int ParkingSpot;
        /// <summary>Game hour at which a parked trucker leaves.</summary>
        public float ParkUntilHour;
        /// <summary>1-based index in RegularCatalog; 0 = a stranger.</summary>
        public byte RegularId;
        /// <summary>People besides the driver who go to the shop (tour bus).</summary>
        public byte Passengers;
        /// <summary>How many of them are still out of the car.</summary>
        public byte PeopleAway;
        /// <summary>Id of the contract this vehicle comes for; 0 = none.</summary>
        public int ContractId;
        /// <summary>Which state the license plate is from (PlateMath).</summary>
        public byte Plate;
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
