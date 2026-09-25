using Unity.Entities;

namespace GasStation.Components
{
    public struct Economy : IComponentData
    {
        public float Money;
        /// <summary>0..1, affects traffic.</summary>
        public float Reputation;
        public float DailyFixedCosts;
        public float DayIncome;
        public float DayExpenses;
        public int DayServed;
        public int DayLost;
        public float DayRatingSum;
        public int DayRatingCount;
    }

    /// <summary>One finished day for the finance charts. Kept for the last DayHistoryLength days.</summary>
    [InternalBufferCapacity(0)]
    public struct DayHistoryEntry : IBufferElementData
    {
        public const int MaxLength = 30;

        public int Day;
        public float Income;
        public float Expenses;
        public int Served;
        public int Lost;
        /// <summary>Average review stars of the day; 0 when there were no reviews.</summary>
        public float Rating;
    }

    /// <summary>Summary of the last finished day. Day == 0 means no day has finished yet.</summary>
    public struct DayReport : IComponentData
    {
        public int Day;
        public float Income;
        public float Expenses;
        public int Served;
        public int Lost;
    }

    public struct GameTime : IComponentData
    {
        /// <summary>Hour of day, 0..24.</summary>
        public float Hour;
        public int Day;
        /// <summary>Game minutes that pass per real second.</summary>
        public float MinutesPerSecond;
    }

    public struct StationSettings : IComponentData
    {
        public float FuelDeliveryTime;
        public float InteractionRadius;
        public int DirtyThreshold;
    }
}
