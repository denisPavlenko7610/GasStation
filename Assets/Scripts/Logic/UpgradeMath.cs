using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public static class UpgradeMath
    {
        public const float TankBonusPerLevel = 1000f;

        /// <summary>
        /// Attendant, Janitor and Mechanic used to be upgrades; they are hired staff now (StaffMath).
        /// They keep their enum values so old saves still map correctly, but can no longer be bought.
        /// </summary>
        public static bool IsLegacyStaff(UpgradeType type) =>
            type is UpgradeType.Attendant or UpgradeType.Janitor or UpgradeType.Mechanic;

        public static int MaxLevel(UpgradeType type) =>
            IsLegacyStaff(type) ? 0 : type is UpgradeType.ExtraPump or UpgradeType.TruckParking ? 2 : 3;

        /// <summary>Upgrades shown in the shop, in display order.</summary>
        public static readonly UpgradeType[] Purchasable =
        {
            UpgradeType.PumpSpeed, UpgradeType.TankCapacity, UpgradeType.Comfort, UpgradeType.Advertising,
            UpgradeType.ExtraPump, UpgradeType.SupplyManager, UpgradeType.CarWash, UpgradeType.TruckParking,
            UpgradeType.TireService, UpgradeType.Motel
        };

        public static StaffRole LegacyRole(UpgradeType type) => type switch
        {
            UpgradeType.Janitor => StaffRole.Janitor,
            UpgradeType.Mechanic => StaffRole.Mechanic,
            _ => StaffRole.Attendant
        };

        public static float BaseCost(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => 400f,
            UpgradeType.TankCapacity => 600f,
            UpgradeType.Comfort => 500f,
            UpgradeType.Advertising => 700f,
            UpgradeType.Attendant => 800f,
            UpgradeType.ExtraPump => 1500f,
            UpgradeType.Janitor => 600f,
            UpgradeType.Mechanic => 900f,
            UpgradeType.CarWash => 1200f,
            UpgradeType.SupplyManager => 700f,
            UpgradeType.TruckParking => 900f,
            UpgradeType.TireService => 800f,
            UpgradeType.Motel => 2000f,
            _ => 1000f
        };

        /// <summary>Price of buying the level after currentLevel. Grows 1.8x per level.</summary>
        public static float Cost(UpgradeType type, int currentLevel) =>
            math.round(BaseCost(type) * math.pow(1.8f, currentLevel));

        public static bool CanUpgrade(UpgradeType type, int currentLevel) => currentLevel < MaxLevel(type);

        public static float FlowMultiplier(int level) => 1f + 0.5f * level;

        public static float PatienceMultiplier(int level) => 1f + 0.2f * level;

        public static float TrafficMultiplier(int level) => 1f + 0.25f * level;
    }
}
