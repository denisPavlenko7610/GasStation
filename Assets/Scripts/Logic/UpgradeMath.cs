using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public static class UpgradeMath
    {
        public const float TankBonusPerLevel = 1000f;
        public const float AttendantSalaryPerLevel = 80f;

        public static int MaxLevel(UpgradeType type) => type == UpgradeType.ExtraPump ? 2 : 3;

        public static float BaseCost(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => 400f,
            UpgradeType.TankCapacity => 600f,
            UpgradeType.Comfort => 500f,
            UpgradeType.Advertising => 700f,
            UpgradeType.Attendant => 800f,
            UpgradeType.ExtraPump => 1500f,
            _ => 1000f
        };

        /// <summary>Price of buying the level after currentLevel. Grows 1.8x per level.</summary>
        public static float Cost(UpgradeType type, int currentLevel) =>
            math.round(BaseCost(type) * math.pow(1.8f, currentLevel));

        public static bool CanUpgrade(UpgradeType type, int currentLevel) => currentLevel < MaxLevel(type);

        public static float FlowMultiplier(int level) => 1f + 0.5f * level;

        public static float PatienceMultiplier(int level) => 1f + 0.2f * level;

        public static float TrafficMultiplier(int level) => 1f + 0.25f * level;

        /// <summary>Seconds a car waits before a hired attendant starts fueling it.</summary>
        public static float AttendantDelay(int level) => level > 0 ? 8f / level : float.PositiveInfinity;
    }
}
