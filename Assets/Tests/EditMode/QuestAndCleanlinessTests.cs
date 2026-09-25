using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class QuestAndCleanlinessTests
    {
        [Test]
        public void FirstQuest_IsTrashCleanup()
        {
            var quest = QuestCatalog.Get(0);
            Assert.AreEqual(QuestGoal.CollectTrash, quest.Goal);
            Assert.IsFalse(quest.IsDaily);
        }

        [Test]
        public void AfterStory_QuestsAreDailyAndGetHarder()
        {
            int first = QuestCatalog.StoryCount;
            var early = QuestCatalog.Get(first);
            var later = QuestCatalog.Get(first + QuestCatalog.DailyGoalCount);

            Assert.IsTrue(early.IsDaily);
            Assert.AreEqual(early.Goal, later.Goal);
            Assert.Greater(later.Target, early.Target);
        }

        [Test]
        public void EveryQuest_HasTargetAndReward()
        {
            for (int i = 0; i < QuestCatalog.StoryCount + 10; i++)
            {
                var quest = QuestCatalog.Get(i);
                Assert.Greater(quest.Target, 0f, $"quest {i}");
                Assert.Greater(quest.RewardMoney + quest.RewardReputation, 0f, $"quest {i}");
            }
        }

        [Test]
        public void Progress_CounterAndStateGoals()
        {
            var trash = new QuestDefinition { Goal = QuestGoal.CollectTrash, Target = 10f };
            var clean = new QuestDefinition { Goal = QuestGoal.Cleanliness, Target = 80f };

            Assert.AreEqual(4f, QuestCatalog.Progress(trash, 4f, 0.5f, 0.5f, 0f, 0, 1, 0));
            Assert.AreEqual(79f, QuestCatalog.Progress(clean, 0f, 0.799f, 0.5f, 0f, 0, 1, 0));
            Assert.IsFalse(QuestCatalog.IsComplete(clean, 79f));
            Assert.IsTrue(QuestCatalog.IsComplete(clean, 80f));
        }

        [TestCase(0, 40, ExpectedResult = 1f)]
        [TestCase(20, 40, ExpectedResult = 0.5f)]
        [TestCase(100, 40, ExpectedResult = 0f)]
        public float Cleanliness_DropsWithLitter(int trash, int threshold) => StationMath.Cleanliness(trash, threshold);

        [Test]
        public void DirtyStation_LosesTrafficAndReputation()
        {
            Assert.Less(StationMath.CleanlinessTrafficFactor(0f), StationMath.CleanlinessTrafficFactor(1f));
            Assert.Less(StationMath.CleanlinessReputationDrift(0f), 0f);
            Assert.Greater(StationMath.CleanlinessReputationDrift(1f), 0f);
        }

        [Test]
        public void JanitorUpgrade_IsStored()
        {
            var upgrades = new StationUpgrades();
            upgrades.Set(UpgradeType.Janitor, 2);
            Assert.AreEqual(2, upgrades.Janitor);
            Assert.Less(StaffMath.JanitorInterval(2f), StaffMath.JanitorInterval(1f));
        }
    }
}
