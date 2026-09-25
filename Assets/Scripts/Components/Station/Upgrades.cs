using Unity.Entities;

namespace GasStation.Components
{
    public enum UpgradeType : byte
    {
        PumpSpeed = 0,
        TankCapacity = 1,
        Comfort = 2,
        Advertising = 3,
        Attendant = 4,
        ExtraPump = 5
    }

    public static class UpgradeTypes
    {
        public const int Count = 6;
    }

    public struct StationUpgrades : IComponentData
    {
        public int PumpSpeed;
        public int TankCapacity;
        public int Comfort;
        public int Advertising;
        public int Attendant;
        public int ExtraPump;

        public int Get(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => PumpSpeed,
            UpgradeType.TankCapacity => TankCapacity,
            UpgradeType.Comfort => Comfort,
            UpgradeType.Advertising => Advertising,
            UpgradeType.Attendant => Attendant,
            UpgradeType.ExtraPump => ExtraPump,
            _ => 0
        };

        public void Set(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.PumpSpeed: PumpSpeed = level; break;
                case UpgradeType.TankCapacity: TankCapacity = level; break;
                case UpgradeType.Comfort: Comfort = level; break;
                case UpgradeType.Advertising: Advertising = level; break;
                case UpgradeType.Attendant: Attendant = level; break;
                case UpgradeType.ExtraPump: ExtraPump = level; break;
            }
        }
    }
}
