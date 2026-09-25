using Unity.Entities;

namespace GasStation.Components
{
    public enum ContractType : byte
    {
        /// <summary>Taxis twice a day, a small early contract.</summary>
        TaxiFleet = 0,
        /// <summary>Patrol cars at a discount; while it runs, thieves avoid the station.</summary>
        Police = 1,
        /// <summary>Delivery vans every two hours.</summary>
        Delivery = 2,
        /// <summary>Three buses every morning, lots of diesel.</summary>
        BusCompany = 3
    }

    public static class ContractTypes
    {
        public const int Count = 4;
    }

    /// <summary>An offer waiting in the laptop mail. Lives in a buffer on the station.</summary>
    [InternalBufferCapacity(0)]
    public struct ContractOffer : IBufferElementData
    {
        public int Id;
        public ContractType Type;
        /// <summary>Fixed price per liter for the whole contract.</summary>
        public float Price;
        public int Days;
        /// <summary>Days until the offer expires.</summary>
        public int ExpiresIn;
    }

    /// <summary>An accepted contract. Its vehicles come on schedule and must not be kept waiting.</summary>
    [InternalBufferCapacity(0)]
    public struct Contract : IBufferElementData
    {
        public int Id;
        public ContractType Type;
        public float Price;
        public int DaysLeft;
        public int Served;
        public int Missed;
        /// <summary>Last game hour vehicles were sent for, so each slot is sent once.</summary>
        public int LastSentHour;
        public int LastSentDay;
    }

    /// <summary>Offer generator state.</summary>
    public struct ContractBoard : IComponentData
    {
        public int NextId;
        public int DaysToNextOffer;
        public Unity.Mathematics.Random Random;
    }

    /// <summary>Where the office laptop stands; walk up and press E.</summary>
    public struct Laptop : IComponentData
    {
        public Unity.Mathematics.float3 Position;
    }
}
