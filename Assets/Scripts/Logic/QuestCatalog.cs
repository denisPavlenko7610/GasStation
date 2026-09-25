using Unity.Mathematics;

namespace GasStation.Logic
{
    public enum QuestGoal : byte
    {
        /// <summary>Counter: pieces of litter picked up since the quest started.</summary>
        CollectTrash,
        /// <summary>Counter: customers who paid.</summary>
        ServeCustomers,
        /// <summary>Counter: fuel orders placed.</summary>
        OrderFuel,
        /// <summary>Counter: upgrades bought.</summary>
        BuyUpgrade,
        /// <summary>State: cleanliness in percent.</summary>
        Cleanliness,
        /// <summary>State: reputation in percent.</summary>
        Reputation,
        /// <summary>State: income of the current day.</summary>
        DayIncome,
        /// <summary>State: ExtraPump upgrade level.</summary>
        OpenPumps,
        /// <summary>Counter: pumps fully repaired.</summary>
        RepairPump,
        /// <summary>State: station level.</summary>
        StationLevel,
        /// <summary>Counter: thieves caught at the pump.</summary>
        CatchThief,
        /// <summary>Counter: products sold in the shop.</summary>
        SellProducts,
        /// <summary>State: CarWash upgrade level.</summary>
        OpenCarWash,
        /// <summary>Counter: restroom cleanings.</summary>
        CleanRestroom,
        /// <summary>Counter: nights paid by parked truckers.</summary>
        HostTruckers,
        /// <summary>State: paint scheme number (0 = peeling paint).</summary>
        PaintStation,
        /// <summary>Counter: tire jobs done.</summary>
        ChangeTires,
        /// <summary>Counter: workers hired.</summary>
        HireWorker,
        /// <summary>Counter: motel guests who paid.</summary>
        HostGuests,
        /// <summary>Counter: renovations finished.</summary>
        Renovate
    }

    public struct QuestDefinition
    {
        public int Id;
        public QuestGoal Goal;
        public float Target;
        public float RewardMoney;
        public float RewardReputation;
        public bool IsDaily;

        public bool IsCounter => Goal is QuestGoal.CollectTrash or QuestGoal.ServeCustomers
            or QuestGoal.OrderFuel or QuestGoal.BuyUpgrade or QuestGoal.RepairPump or QuestGoal.CatchThief
            or QuestGoal.SellProducts or QuestGoal.CleanRestroom or QuestGoal.HostTruckers
            or QuestGoal.ChangeTires or QuestGoal.HireWorker
            or QuestGoal.HostGuests or QuestGoal.Renovate;
    }

    /// <summary>
    /// Story quests that walk the player from an abandoned, littered station to a working business,
    /// followed by endless daily quests. Texts live in GameTexts, keyed by Id.
    /// </summary>
    public static class QuestCatalog
    {
        public const int DailyIdBase = 100;

        private static readonly QuestDefinition[] Story =
        {
            Quest(0, QuestGoal.CollectTrash, 10f, 200f),
            Quest(11, QuestGoal.RepairPump, 1f, 150f),
            Quest(22, QuestGoal.Renovate, 1f, 100f),
            Quest(1, QuestGoal.ServeCustomers, 3f, 150f),
            Quest(14, QuestGoal.SellProducts, 5f, 150f),
            Quest(16, QuestGoal.CleanRestroom, 1f, 100f),
            Quest(2, QuestGoal.Cleanliness, 80f, 0f, 0.05f),
            Quest(3, QuestGoal.OrderFuel, 1f, 100f),
            Quest(4, QuestGoal.BuyUpgrade, 1f, 250f),
            Quest(18, QuestGoal.PaintStation, 1f, 200f, 0.05f),
            Quest(20, QuestGoal.HireWorker, 1f, 200f),
            Quest(5, QuestGoal.ServeCustomers, 15f, 400f),
            Quest(6, QuestGoal.CollectTrash, 40f, 400f),
            Quest(7, QuestGoal.Reputation, 70f, 500f),
            Quest(23, QuestGoal.Renovate, 3f, 400f, 0.05f),
            Quest(8, QuestGoal.DayIncome, 1000f, 800f),
            Quest(12, QuestGoal.StationLevel, 4f, 600f),
            Quest(9, QuestGoal.OpenPumps, 1f, 1000f),
            Quest(15, QuestGoal.OpenCarWash, 1f, 800f),
            Quest(17, QuestGoal.HostTruckers, 3f, 600f),
            Quest(21, QuestGoal.HostGuests, 5f, 1200f, 0.05f),
            Quest(19, QuestGoal.ChangeTires, 3f, 500f),
            Quest(13, QuestGoal.CatchThief, 1f, 500f, 0.05f),
            Quest(10, QuestGoal.Cleanliness, 100f, 500f, 0.05f),
        };

        private static readonly QuestGoal[] DailyGoals =
        {
            QuestGoal.CollectTrash,
            QuestGoal.ServeCustomers,
            QuestGoal.SellProducts,
            QuestGoal.DayIncome
        };

        public static int StoryCount => Story.Length;

        public static int DailyGoalCount => DailyGoals.Length;

        public static QuestDefinition Get(int index)
        {
            if (index < Story.Length)
                return Story[index];

            // Daily quests cycle through goals and get harder over time.
            int daily = index - Story.Length;
            var goal = DailyGoals[daily % DailyGoals.Length];
            float difficulty = 1f + 0.25f * (daily / DailyGoals.Length);
            float target = goal switch
            {
                QuestGoal.CollectTrash => math.round(15f * difficulty),
                QuestGoal.ServeCustomers => math.round(20f * difficulty),
                QuestGoal.SellProducts => math.round(25f * difficulty),
                _ => math.round(1200f * difficulty / 100f) * 100f
            };

            return new QuestDefinition
            {
                Id = DailyIdBase + (int)goal,
                Goal = goal,
                Target = target,
                RewardMoney = math.round(300f * difficulty / 10f) * 10f,
                IsDaily = true
            };
        }

        /// <summary>Progress towards the goal. Counter goals use the stored counter, state goals read the station.</summary>
        public static float Progress(QuestDefinition quest, float counter, float cleanliness, float reputation,
            float dayIncome, int openPumps, int stationLevel, int carWashLevel, int paintScheme = 1)
        {
            return quest.Goal switch
            {
                QuestGoal.Cleanliness => math.floor(cleanliness * 100f),
                QuestGoal.Reputation => math.floor(reputation * 100f),
                QuestGoal.DayIncome => dayIncome,
                QuestGoal.OpenPumps => openPumps,
                QuestGoal.StationLevel => stationLevel,
                QuestGoal.OpenCarWash => carWashLevel,
                QuestGoal.PaintStation => paintScheme,
                _ => counter
            };
        }

        public static bool IsComplete(QuestDefinition quest, float progress) => progress >= quest.Target;

        private static QuestDefinition Quest(int id, QuestGoal goal, float target, float money, float reputation = 0f) =>
            new() { Id = id, Goal = goal, Target = target, RewardMoney = money, RewardReputation = reputation };
    }
}
