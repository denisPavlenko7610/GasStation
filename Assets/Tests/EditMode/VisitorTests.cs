using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class VisitorTests
    {
        [Test]
        public void Catalog_HasTwentyRegulars_WithTexts()
        {
            Assert.AreEqual(20, RegularCatalog.Count);
            for (int i = 0; i < RegularCatalog.Count; i++)
            {
                foreach (var suffix in new[] { "name", "about", "line.0", "line.1" })
                    Assert.IsTrue(LocTable.Entries.ContainsKey($"regular.{i}.{suffix}"), $"regular.{i}.{suffix}");
                Assert.AreNotEqual(0, RegularCatalog.Get(i).Days, $"regular {i} never comes");
                Assert.Greater(RegularCatalog.Get(i).Liters, 0f);
            }
        }

        [Test]
        public void Weekdays_StartOnDayOne()
        {
            Assert.AreEqual(0, RegularCatalog.Weekday(1));
            Assert.AreEqual(6, RegularCatalog.Weekday(7));
            Assert.AreEqual(0, RegularCatalog.Weekday(8));

            var mondays = new RegularInfo { Days = 1, Hour = 6 };
            Assert.IsTrue(RegularCatalog.ComesOn(mondays, 1));
            Assert.IsFalse(RegularCatalog.ComesOn(mondays, 2));
            Assert.IsTrue(RegularCatalog.ComesOn(mondays, 15));
        }

        [Test]
        public void Window_IsTwoHours_AndWrapsMidnight()
        {
            var late = new RegularInfo { Hour = 23 };
            Assert.IsTrue(RegularCatalog.InWindow(late, 23));
            Assert.IsTrue(RegularCatalog.InWindow(late, 0));
            Assert.IsFalse(RegularCatalog.InWindow(late, 1));
            Assert.IsFalse(RegularCatalog.InWindow(late, 22));
        }

        [Test]
        public void Loyalty_GrowsWithGoodVisits_AndDropsFasterWithBadOnes()
        {
            float up = VisitorMath.NextLoyalty(0.5f, 5f) - 0.5f;
            float down = 0.5f - VisitorMath.NextLoyalty(0.5f, 1f);
            Assert.Greater(up, 0f);
            Assert.Greater(down, up);
            Assert.AreEqual(0.5f, VisitorMath.NextLoyalty(0.5f, 3f), 0.0001f);
            Assert.AreEqual(1f, VisitorMath.NextLoyalty(0.99f, 5f), 0.0001f);
            Assert.AreEqual(0f, VisitorMath.NextLoyalty(0.05f, 1f), 0.0001f);
        }

        [Test]
        public void LoyalRegulars_ComeMoreOften_AndTip()
        {
            Assert.Greater(VisitorMath.VisitChance(1f), VisitorMath.VisitChance(0f));
            Assert.AreEqual(0f, VisitorMath.TipShare(0.3f));
            Assert.Greater(VisitorMath.TipShare(1f), 0f);
            Assert.Greater(VisitorMath.FriendsTrafficFactor(3), 1f);
        }

        [Test]
        public void Critic_WritesOnlyAboutExtremes()
        {
            Assert.Greater(VisitorMath.ArticleFactor(5f), 1f);
            Assert.Less(VisitorMath.ArticleFactor(1f), 1f);
            Assert.AreEqual(1f, VisitorMath.ArticleFactor(3f));
        }

        [Test]
        public void SpecialGuests_AreNeverPickedAtRandom()
        {
            for (float r = 0f; r < 1f; r += 0.01f)
            {
                var type = CustomerProfiles.Pick(r, 10, 12f, true);
                Assert.Less((int)type, CustomerProfiles.Count, type.ToString());
            }

            Assert.AreEqual(Enum.GetValues(typeof(CustomerType)).Length, CustomerProfiles.AllCount);
        }

        [Test]
        public void SpecialProfiles_MatchTheirRole()
        {
            Assert.IsTrue(CustomerProfiles.Get(CustomerType.TourBus).DieselOnly);
            Assert.AreEqual(1f, CustomerProfiles.Get(CustomerType.TourBus).ShopChance);
            Assert.AreEqual(1f, CustomerProfiles.Get(CustomerType.TowTruck).TireChance);
            Assert.Less(CustomerProfiles.Get(CustomerType.Emergency).PatienceMultiplier, 1f);
            Assert.Greater(CustomerProfiles.Get(CustomerType.Biker).LitterMultiplier, 1f);
        }

        [Test]
        public void RegularSave_RoundTrips()
        {
            var regular = new RegularState { Loyalty = 0.8f, Visits = 12, Upsets = 1, Lost = false, LastVisitDay = 9, LastStars = 4f };
            var json = JsonUtility.ToJson(RegularSaveData.From(regular));
            var restored = JsonUtility.FromJson<RegularSaveData>(json).ToState();
            Assert.AreEqual(0.8f, restored.Loyalty, 0.0001f);
            Assert.AreEqual(12, restored.Visits);
            Assert.AreEqual(1, restored.Upsets);
            Assert.AreEqual(9, restored.LastVisitDay);
        }

        [Test]
        public void EveryWeekday_AndSpecialGuest_IsLocalized()
        {
            for (int i = 0; i < 7; i++)
                Assert.IsTrue(LocTable.Entries.ContainsKey($"weekday.{i}"));
            foreach (var type in new[] { CustomerType.TourBus, CustomerType.Biker, CustomerType.Emergency, CustomerType.TowTruck })
                Assert.IsTrue(LocTable.Entries.ContainsKey($"msg.special.{type}"), type.ToString());
        }
    }
}
