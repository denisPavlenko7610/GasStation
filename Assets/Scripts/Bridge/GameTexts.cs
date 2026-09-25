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
            UpgradeType.SupplyManager => "Завхоз",
            UpgradeType.TruckParking => "Стоянка для фур",
            UpgradeType.TireService => "Шиномонтаж",
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
            UpgradeType.SupplyManager => "сам заказывает топливо и товар; ур.2 −10%, ур.3 быстрее; $50/день",
            UpgradeType.TruckParking => "+2 места для ночёвки дальнобойщиков",
            UpgradeType.TireService => "меняете шины за деньги; уровни — быстрее и дороже",
            _ => string.Empty
        };

        private static readonly string[] ProductNames = { "Вода", "Кофе", "Снеки", "Моторное масло", "Сувениры" };

        public static string ProductName(ProductType type) => ProductNames[(int)type];

        private static readonly string[] StaffNames =
        {
            "Иван", "Ольга", "Пётр", "Анна", "Сергей", "Мария", "Алексей", "Елена",
            "Дмитрий", "Наталья", "Андрей", "Татьяна", "Михаил", "Ирина", "Николай", "Светлана"
        };

        public static string StaffName(int index) => StaffNames[((index % StaffNames.Length) + StaffNames.Length) % StaffNames.Length];

        public static string RoleName(StaffRole role) => role switch
        {
            StaffRole.Attendant => "заправщик",
            StaffRole.Janitor => "уборщик",
            StaffRole.Mechanic => "механик",
            StaffRole.Cashier => "кассир",
            _ => role.ToString()
        };

        public static string RoleDuty(StaffRole role) => role switch
        {
            StaffRole.Attendant => "сам заправляет машины",
            StaffRole.Janitor => "убирает мусор и туалет",
            StaffRole.Mechanic => "чинит колонки, меняет шины",
            StaffRole.Cashier => "ускоряет покупки в магазине",
            _ => string.Empty
        };

        public static string ReferenceText(int grade) => grade switch
        {
            2 => "отличные рекомендации",
            1 => "обычные рекомендации",
            _ => "сомнительные рекомендации"
        };

        public static string SchemeName(int scheme) => scheme switch
        {
            0 => "Облезлая краска",
            1 => "Классика",
            2 => "Пустынный закат",
            3 => "Неон 80-х",
            _ => scheme.ToString()
        };

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
            16 => "Туалет",
            17 => "Ночлег",
            18 => "Новый облик",
            19 => "Шиномонтаж",
            20 => "Первый сотрудник",
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
            QuestGoal.HireWorker => "Найми сотрудника (H): {0}/1",
            QuestGoal.PaintStation => "Перекрась станцию (C): {0}/1",
            QuestGoal.ChangeTires => $"Поменяй шины клиентам (E у бокса шиномонтажа): {{0}}/{quest.Target:0}",
            QuestGoal.CleanRestroom => "Убери туалет (E у двери): {0}/1",
            QuestGoal.HostTruckers => $"Прими дальнобойщиков на ночь (Стоянка для фур): {{0}}/{quest.Target:0}",
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
