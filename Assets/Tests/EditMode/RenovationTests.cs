using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class RenovationTests
    {
        [Test]
        public void EveryKind_HasCostBonusAndText()
        {
            foreach (RenovationKind kind in Enum.GetValues(typeof(RenovationKind)))
            {
                Assert.Greater(RenovationMath.Cost(kind), 0f, kind.ToString());
                Assert.Greater(RenovationMath.TrafficBonus(kind), 0f, kind.ToString());
                Assert.GreaterOrEqual(RenovationMath.RequiredLevel(kind), 1, kind.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"renovation.{kind}"), kind.ToString());
            }
        }

        [Test]
        public void FullyRenovated_NeedsEveryRenovation()
        {
            var context = new AchievementContext { RenovationsTotal = 6, RenovationsDone = 5 };
            Assert.IsFalse(AchievementCatalog.IsReached(AchievementId.FullyRenovated, context));
            context.RenovationsDone = 6;
            Assert.IsTrue(AchievementCatalog.IsReached(AchievementId.FullyRenovated, context));
        }

        [Test]
        public void FullyRenovated_IsNotGrantedWithoutRenovations()
        {
            Assert.IsFalse(AchievementCatalog.IsReached(AchievementId.FullyRenovated, new AchievementContext()));
        }

        [Test]
        public void RenovationQuests_ArePartOfTheStory()
        {
            bool found = false;
            for (int i = 0; i < QuestCatalog.StoryCount; i++)
                found |= QuestCatalog.Get(i).Goal == QuestGoal.Renovate;
            Assert.IsTrue(found);
        }

        [Test]
        public void DoneRenovations_SurviveSaveRoundTrip()
        {
            var data = new SaveData { renovationsDone = new[] { 0, 3, 5 } };
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            CollectionAssert.AreEqual(new[] { 0, 3, 5 }, loaded.renovationsDone);
        }
    }
}
