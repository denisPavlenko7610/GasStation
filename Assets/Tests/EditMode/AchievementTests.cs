using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class AchievementTests
    {
        [Test]
        public void CatalogCount_MatchesEnum_AndFitsTheBitmask()
        {
            Assert.AreEqual(Enum.GetValues(typeof(AchievementId)).Length, AchievementCatalog.Count);
            Assert.LessOrEqual(AchievementCatalog.Count, 64);
        }

        [Test]
        public void EveryAchievement_HasNameAndDescription()
        {
            foreach (AchievementId id in Enum.GetValues(typeof(AchievementId)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"achievement.{id}.name"), id.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"achievement.{id}.desc"), id.ToString());
            }
        }

        [Test]
        public void Count_TracksServedAndIncome()
        {
            var stats = new StationStats();
            AchievementCatalog.Count(ref stats, StationEventType.CustomerPaid, 30f);
            AchievementCatalog.Count(ref stats, StationEventType.TipReceived, 5f);
            AchievementCatalog.Count(ref stats, StationEventType.QuestCompleted, 500f);
            AchievementCatalog.Count(ref stats, StationEventType.TrashCollected, 0f);

            Assert.AreEqual(1, stats.Served);
            Assert.AreEqual(1, stats.TrashCollected);
            Assert.AreEqual(35f, stats.Income, 1e-4f, "quest rewards are not customer income");
        }

        [Test]
        public void FirstCustomer_IsReachedAfterOneCustomer()
        {
            var context = new AchievementContext();
            Assert.IsFalse(AchievementCatalog.IsReached(AchievementId.FirstCustomer, context));
            context.Stats.Served = 1;
            Assert.IsTrue(AchievementCatalog.IsReached(AchievementId.FirstCustomer, context));
        }

        [Test]
        public void PerfectDay_NeedsTwentyCustomersAndNoLosses()
        {
            Assert.IsTrue(AchievementCatalog.IsPerfectDay(new DayReport { Served = 20, Lost = 0 }));
            Assert.IsFalse(AchievementCatalog.IsPerfectDay(new DayReport { Served = 20, Lost = 1 }));
            Assert.IsFalse(AchievementCatalog.IsPerfectDay(new DayReport { Served = 19, Lost = 0 }));
        }

        [Test]
        public void Achievements_Has_ReadsBits()
        {
            var achievements = new Achievements { Unlocked = (1UL << 3) | (1UL << 17) };
            Assert.IsTrue(achievements.Has(3));
            Assert.IsTrue(achievements.Has(17));
            Assert.IsFalse(achievements.Has(4));
        }

        [Test]
        public void StatsAndAchievements_SurviveSaveRoundTrip()
        {
            var data = new SaveData
            {
                stats = StatsSaveData.From(new StationStats { Served = 42, Income = 1234f, PerfectDays = 2 }),
                achievements = (long)((1UL << 0) | (1UL << 5))
            };

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.AreEqual(42, loaded.stats.ToStats().Served);
            Assert.AreEqual(2, loaded.stats.ToStats().PerfectDays);
            Assert.IsTrue(new Achievements { Unlocked = (ulong)loaded.achievements }.Has(5));
        }
    }
}
