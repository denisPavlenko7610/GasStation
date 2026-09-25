using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Station level, equipment wear and fuel market formulas.</summary>
    public static class ProgressMath
    {
        public const int MaxLevel = 10;

        public const float WearPerLiter = 0.0004f;
        public const float RepairStep = 0.25f;
        public const float RepairStepCost = 40f;
        /// <summary>Below this condition the player can repair a pump.</summary>
        public const float RepairThreshold = 0.75f;

        public const float InspectionReward = 150f;
        public const float InspectionFine = 200f;
        public const float InspectionCleanlinessRequired = 0.7f;

        /// <summary>Experience needed to go from level to level + 1.</summary>
        public static float ExperienceToNext(int level) => 100f * level;

        public static float Experience(StationEventType type) => type switch
        {
            StationEventType.CustomerPaid => 10f,
            StationEventType.TrashCollected => 2f,
            StationEventType.QuestCompleted => 50f,
            StationEventType.UpgradeBought => 20f,
            StationEventType.PumpRepaired => 15f,
            StationEventType.ThiefCaught => 30f,
            StationEventType.InspectionPassed => 40f,
            StationEventType.ShopSale => 3f,
            StationEventType.CarWashed => 5f,
            StationEventType.ParkingPaid => 8f,
            StationEventType.RestroomCleaned => 5f,
            StationEventType.TiresChanged => 8f,
            StationEventType.StationPainted => 30f,
            StationEventType.WorkerHired => 10f,
            StationEventType.RenovationDone => 25f,
            StationEventType.MotelPaid => 12f,
            StationEventType.MotelRoomCleaned => 3f,
            _ => 0f
        };

        /// <summary>Adds experience; returns the number of levels gained.</summary>
        public static int AddExperience(ref StationLevel level, float experience)
        {
            int gained = 0;
            level.Experience += experience;
            while (level.Level < MaxLevel && level.Experience >= ExperienceToNext(level.Level))
            {
                level.Experience -= ExperienceToNext(level.Level);
                level.Level++;
                gained++;
            }

            return gained;
        }

        /// <summary>+5% traffic per level above 1.</summary>
        public static float LevelTrafficFactor(int level) => 1f + 0.05f * (math.max(1, level) - 1);

        /// <summary>Station level needed to buy the next level of an upgrade.</summary>
        public static int RequiredLevel(UpgradeType type, int currentUpgradeLevel)
        {
            int baseLevel = type switch
            {
                UpgradeType.PumpSpeed => 1,
                UpgradeType.TankCapacity => 2,
                UpgradeType.Comfort => 2,
                UpgradeType.Janitor => 2,
                UpgradeType.Advertising => 3,
                UpgradeType.Attendant => 3,
                UpgradeType.Mechanic => 4,
                UpgradeType.ExtraPump => 4,
                UpgradeType.CarWash => 3,
                UpgradeType.SupplyManager => 2,
                UpgradeType.TruckParking => 3,
                UpgradeType.TireService => 2,
                UpgradeType.Motel => 4,
                _ => 1
            };

            return baseLevel + currentUpgradeLevel;
        }

        /// <summary>Tomorrow's market price: a random walk of up to ±8%, pulled back towards the base price.</summary>
        public static float NextMarketPrice(float current, float basePrice, float random01)
        {
            float change = (random01 * 2f - 1f) * 0.08f;
            float pull = (basePrice - current) / basePrice * 0.25f;
            float next = current * (1f + change + pull);
            return math.clamp(next, basePrice * 0.7f, basePrice * 1.5f);
        }

        /// <summary>Wholesale price the station pays: 72% of the market price.</summary>
        public static float BuyPrice(float marketPrice) => math.round(marketPrice * 0.72f * 100f) / 100f;
    }
}
