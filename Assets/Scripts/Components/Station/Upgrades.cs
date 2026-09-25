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
        ExtraPump = 5,
        Janitor = 6,
        Mechanic = 7,
        CarWash = 8,
        SupplyManager = 9,
        TruckParking = 10,
        TireService = 11,
        Motel = 12
    }

    public static class UpgradeTypes
    {
        public const int Count = 13;
    }

    public struct StationUpgrades : IComponentData
    {
        public int PumpSpeed;
        public int TankCapacity;
        public int Comfort;
        public int Advertising;
        public int Attendant;
        public int ExtraPump;
        public int Janitor;
        public int Mechanic;
        public int CarWash;
        public int SupplyManager;
        public int TruckParking;
        public int TireService;
        public int Motel;

        public int Get(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => PumpSpeed,
            UpgradeType.TankCapacity => TankCapacity,
            UpgradeType.Comfort => Comfort,
            UpgradeType.Advertising => Advertising,
            UpgradeType.Attendant => Attendant,
            UpgradeType.ExtraPump => ExtraPump,
            UpgradeType.Janitor => Janitor,
            UpgradeType.Mechanic => Mechanic,
            UpgradeType.CarWash => CarWash,
            UpgradeType.SupplyManager => SupplyManager,
            UpgradeType.TruckParking => TruckParking,
            UpgradeType.TireService => TireService,
            UpgradeType.Motel => Motel,
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
                case UpgradeType.Janitor: Janitor = level; break;
                case UpgradeType.Mechanic: Mechanic = level; break;
                case UpgradeType.CarWash: CarWash = level; break;
                case UpgradeType.SupplyManager: SupplyManager = level; break;
                case UpgradeType.TruckParking: TruckParking = level; break;
                case UpgradeType.TireService: TireService = level; break;
                case UpgradeType.Motel: Motel = level; break;
            }
        }
    }
}
