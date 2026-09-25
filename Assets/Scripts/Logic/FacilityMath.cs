using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Truck parking, restroom and supply manager rules.</summary>
    public static class FacilityMath
    {
        public const int SpotsPerParkingLevel = 2;
        public const float SupplyManagerSalaryPerLevel = 50f;
        public const float SupplyCheckInterval = 5f;
        /// <summary>The supply manager reorders when stock (with orders on the way) falls below this share.</summary>
        public const float ReorderThreshold = 0.25f;
        /// <summary>The supply manager keeps this much money untouched.</summary>
        public const float MoneyReserve = 200f;

        public const float RestroomDisgustingDirt = 0.7f;
        public const float RestroomCleanThreshold = 0.1f;
        public const float JanitorRestroomCleaningPerSecond = 0.005f;

        public static int OpenSpots(int parkingLevel, int totalSpots) =>
            math.min(totalSpots, parkingLevel * SpotsPerParkingLevel);

        /// <summary>Truckers look for a place to sleep in the evening and at night.</summary>
        public static bool WantsToSleep(float hour) => hour >= 19f || hour < 4f;

        /// <summary>A parked trucker leaves once the morning departure hour has come (and before noon).</summary>
        public static bool ShouldLeaveParking(float hour, float parkUntilHour) => hour >= parkUntilHour && hour < 12f;

        public static float ParkingFee(float feePerNight, int parkingLevel) =>
            feePerNight * (1f + 0.25f * math.max(0, parkingLevel - 1));

        /// <summary>A dirty restroom makes the whole station feel up to 40% dirtier.</summary>
        public static float CombinedCleanliness(float trashCleanliness, float restroomDirt) =>
            trashCleanliness * (1f - 0.4f * math.saturate(restroomDirt));

        /// <summary>Supply manager level 2 negotiates a 10% discount.</summary>
        public static float SupplyDiscount(int level) => level >= 2 ? 0.9f : 1f;

        /// <summary>Supply manager level 3 halves delivery time.</summary>
        public static float SupplyDeliveryFactor(int level) => level >= 3 ? 0.5f : 1f;

        public const int RoomsPerMotelLevel = 2;
        public const float MotelReputationBonus = 0.01f;

        public static int OpenRooms(int motelLevel, int totalRooms) => math.min(totalRooms, motelLevel * RoomsPerMotelLevel);

        /// <summary>Travellers look for a room in the evening.</summary>
        public static bool WantsRoom(float hour) => hour >= 18f || hour < 3f;

        public static float RoomPrice(float basePrice, int motelLevel) =>
            basePrice * (1f + 0.25f * math.max(0, motelLevel - 1));

        /// <summary>Seconds a janitor needs per dirty room.</summary>
        public static float JanitorRoomInterval(float janitorPower) => janitorPower > 0f ? 30f / janitorPower : float.PositiveInfinity;

        public static bool NeedsReorder(float stockWithPending, float capacity) =>
            capacity > 0f && stockWithPending < capacity * ReorderThreshold;
    }
}
