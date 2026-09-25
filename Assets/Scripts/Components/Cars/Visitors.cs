using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>A car VisitorSystem wants on the road: a regular or a special guest. Lives on the CarSpawner.</summary>
    [InternalBufferCapacity(4)]
    public struct SpawnRequest : IBufferElementData
    {
        public CustomerType Customer;
        public byte RegularId;
        /// <summary>Seconds to wait after the previous request (a biker convoy arrives one by one).</summary>
        public float Delay;
        /// <summary>Set by regulars: their usual fuel and habits instead of random ones.</summary>
        public bool HasHabits;
        public FuelType Fuel;
        public float LitersMultiplier;
        public bool WantsShop;
        public bool WantsWash;
        public byte Passengers;
        /// <summary>Contract vehicle: pays the contract price and jumps the queue.</summary>
        public int ContractId;
    }

    /// <summary>What the station knows about one regular. One entry per RegularCatalog entry, on the station.</summary>
    [InternalBufferCapacity(0)]
    public struct RegularState : IBufferElementData
    {
        /// <summary>0..1. Grows with good visits; loyal regulars come more often, tip more and bring friends.</summary>
        public float Loyalty;
        public int Visits;
        /// <summary>Bad visits in a row; three and the regular goes to the competitor.</summary>
        public int Upsets;
        public bool Lost;
        /// <summary>Day of the last visit (or of the planned one), so a regular comes at most once a day.</summary>
        public int LastVisitDay;
        /// <summary>Stars of the last visit, for the regulars panel.</summary>
        public float LastStars;
    }

    /// <summary>Critic articles: a traffic multiplier for a few days.</summary>
    public struct Buzz : IComponentData
    {
        public float Factor;
        public int DaysLeft;
    }

    /// <summary>Hourly rolls for regulars and special guests.</summary>
    public struct Visitors : IComponentData
    {
        public int LastRolledHour;
        public int LastCriticDay;
        public int LastBusDay;
        public Unity.Mathematics.Random Random;
    }
}
