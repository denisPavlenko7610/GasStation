using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Tracks the current quest from station events and state, pays the reward and moves to the next one.
    /// Runs last so it sees every event raised this frame before HudBridgeSystem drains them.
    /// </summary>
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    public partial class QuestSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<QuestProgress>();
            RequireForUpdate<Economy>();
            RequireForUpdate<StationCleanliness>();
            RequireForUpdate<StationUpgrades>();
            RequireForUpdate<StationEvent>();
        }

        protected override void OnUpdate()
        {
            var station = SystemAPI.GetSingletonEntity<QuestProgress>();
            var progress = SystemAPI.GetComponent<QuestProgress>(station);
            var quest = QuestCatalog.Get(progress.Index);

            var events = SystemAPI.GetBuffer<StationEvent>(station);
            for (int i = 0; i < events.Length; i++)
            {
                if (Counts(quest.Goal, events[i].Type))
                    progress.Counter += 1f;
            }

            var economy = SystemAPI.GetComponent<Economy>(station);
            float current = QuestCatalog.Progress(quest, progress.Counter,
                SystemAPI.GetComponent<StationCleanliness>(station).Value,
                economy.Reputation,
                economy.DayIncome,
                SystemAPI.GetComponent<StationUpgrades>(station).ExtraPump,
                SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1,
                SystemAPI.GetComponent<StationUpgrades>(station).CarWash,
                SystemAPI.HasComponent<StationStyle>(station) ? SystemAPI.GetComponent<StationStyle>(station).Scheme : 1);

            if (QuestCatalog.IsComplete(quest, current))
            {
                economy.Money += quest.RewardMoney;
                economy.Reputation = StationMath.ClampReputation(economy.Reputation + quest.RewardReputation);
                SystemAPI.SetComponent(station, economy);

                StationEvent.Push(events, StationEventType.QuestCompleted, default, quest.RewardMoney);
                HudModel.Notify($"Задание выполнено: {GameTexts.QuestTitle(quest)}! Награда: {GameTexts.QuestReward(quest)}");

                progress.Index++;
                progress.Counter = 0f;
                progress.Completed++;
                quest = QuestCatalog.Get(progress.Index);
                current = 0f;
            }

            SystemAPI.SetComponent(station, progress);

            HudModel.Quest = quest;
            HudModel.QuestProgress = current;
        }

        private static bool Counts(QuestGoal goal, StationEventType type) => goal switch
        {
            QuestGoal.CollectTrash => type == StationEventType.TrashCollected,
            QuestGoal.ServeCustomers => type == StationEventType.CustomerPaid,
            QuestGoal.OrderFuel => type == StationEventType.FuelOrdered,
            QuestGoal.BuyUpgrade => type == StationEventType.UpgradeBought,
            QuestGoal.RepairPump => type == StationEventType.PumpRepaired,
            QuestGoal.CatchThief => type == StationEventType.ThiefCaught,
            QuestGoal.SellProducts => type == StationEventType.ShopSale,
            QuestGoal.CleanRestroom => type == StationEventType.RestroomCleaned,
            QuestGoal.HostTruckers => type == StationEventType.ParkingPaid,
            QuestGoal.ChangeTires => type == StationEventType.TiresChanged,
            _ => false
        };
    }
}
