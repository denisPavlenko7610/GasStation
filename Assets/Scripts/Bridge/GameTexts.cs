using GasStation.Components;
using GasStation.Logic;

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
            UpgradeType.Janitor => "Уборщик",
            UpgradeType.Mechanic => "Механик",
            UpgradeType.CarWash => "Автомойка",
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
            UpgradeType.Janitor => "сам убирает мусор, $60/день",
            UpgradeType.Mechanic => "сам чинит колонки, $70/день",
            UpgradeType.CarWash => "открывает мойку, уровни — быстрее и дороже",
            _ => string.Empty
        };

        private static readonly string[] ProductNames = { "Вода", "Кофе", "Снеки", "Моторное масло", "Сувениры" };

        public static string ProductName(ProductType type) => ProductNames[(int)type];

        public static string CustomerName(CustomerType type) => type switch
        {
            CustomerType.Trucker => "дальнобойщик",
            CustomerType.Hurry => "торопыга",
            CustomerType.Tourist => "турист",
            CustomerType.Thief => "подозрительный тип",
            _ => "клиент"
        };

        public static string QuestTitle(QuestDefinition quest) => quest.Id switch
        {
            0 => "Заброшенная заправка",
            1 => "Первые клиенты",
            2 => "Наводим порядок",
            3 => "Бензовоз",
            4 => "Первое вложение",
            5 => "Постоянные клиенты",
            6 => "Генеральная уборка",
            7 => "Хорошая репутация",
            8 => "Прибыльный день",
            9 => "Расширение",
            10 => "Идеальная чистота",
            11 => "Колонка не работает",
            12 => "Растущая слава",
            13 => "Держи вора!",
            14 => "Первые покупки",
            15 => "Автомойка",
            _ => "Задание дня"
        };

        public static string QuestGoalText(QuestDefinition quest) => quest.Goal switch
        {
            QuestGoal.CollectTrash => $"Собери мусор (E): {{0}}/{quest.Target:0}",
            QuestGoal.ServeCustomers => $"Обслужи клиентов: {{0}}/{quest.Target:0}",
            QuestGoal.OrderFuel => $"Закажи бензовоз (O): {{0}}/{quest.Target:0}",
            QuestGoal.BuyUpgrade => $"Купи улучшение (Tab): {{0}}/{quest.Target:0}",
            QuestGoal.Cleanliness => $"Доведи чистоту до {quest.Target:0}%: сейчас {{0}}%",
            QuestGoal.Reputation => $"Подними репутацию до {quest.Target:0}%: сейчас {{0}}%",
            QuestGoal.DayIncome => $"Заработай за день ${quest.Target:0}: сейчас ${{0}}",
            QuestGoal.OpenPumps => "Открой новую колонку (Tab): {0}/1",
            QuestGoal.RepairPump => $"Почини колонку (E рядом со сломанной): {{0}}/{quest.Target:0}",
            QuestGoal.StationLevel => $"Подними уровень станции до {quest.Target:0}: сейчас {{0}}",
            QuestGoal.SellProducts => $"Продай товары в магазине (M — ассортимент): {{0}}/{quest.Target:0}",
            QuestGoal.OpenCarWash => "Открой автомойку (Tab): {0}/1",
            QuestGoal.CatchThief => "Стой рядом с машиной вора, когда он заправляется: {0}/1",
            _ => "{0}"
        };

        public static string QuestReward(QuestDefinition quest)
        {
            if (quest.RewardMoney > 0f && quest.RewardReputation > 0f)
                return $"${quest.RewardMoney:0} и +{quest.RewardReputation * 100f:0}% репутации";
            return quest.RewardMoney > 0f ? $"${quest.RewardMoney:0}" : $"+{quest.RewardReputation * 100f:0}% репутации";
        }
    }
}
