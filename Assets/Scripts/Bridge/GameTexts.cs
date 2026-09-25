using GasStation.Components;

namespace GasStation.Bridge
{
    public static class GameTexts
    {
        private static readonly string[] FuelNames = { "АИ-92", "АИ-95", "ДТ" };

        public static string FuelName(FuelType type) => FuelNames[(int)type];

        public static string UpgradeName(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => "Быстрые насосы",
            UpgradeType.TankCapacity => "Большие резервуары",
            UpgradeType.Comfort => "Навес и кофе",
            UpgradeType.Advertising => "Реклама",
            UpgradeType.Attendant => "Заправщик",
            UpgradeType.ExtraPump => "Новая колонка",
            _ => type.ToString()
        };

        public static string UpgradeDescription(UpgradeType type) => type switch
        {
            UpgradeType.PumpSpeed => "+50% скорости заправки",
            UpgradeType.TankCapacity => "+1000 л к каждому резервуару",
            UpgradeType.Comfort => "клиенты ждут на 20% дольше",
            UpgradeType.Advertising => "+25% клиентов",
            UpgradeType.Attendant => "сам заправляет машины, $80/день",
            UpgradeType.ExtraPump => "открывает ещё одну колонку",
            _ => string.Empty
        };
    }
}
