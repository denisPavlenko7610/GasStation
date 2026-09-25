using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class ProgressTests
    {
        [Test]
        public void AddExperience_LevelsUpAndCarriesOver()
        {
            var level = new StationLevel { Level = 1 };
            int gained = ProgressMath.AddExperience(ref level, 350f);

            // 100 xp for 1 -> 2, 200 xp for 2 -> 3, 50 left over.
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, level.Level);
            Assert.AreEqual(50f, level.Experience, 1e-4f);
        }

        [Test]
        public void AddExperience_StopsAtMaxLevel()
        {
            var level = new StationLevel { Level = ProgressMath.MaxLevel };
            Assert.AreEqual(0, ProgressMath.AddExperience(ref level, 1e6f));
            Assert.AreEqual(ProgressMath.MaxLevel, level.Level);
        }

        [Test]
        public void RequiredLevel_GrowsWithUpgradeLevel()
        {
            Assert.AreEqual(1, ProgressMath.RequiredLevel(UpgradeType.PumpSpeed, 0));
            Assert.Greater(ProgressMath.RequiredLevel(UpgradeType.PumpSpeed, 2), ProgressMath.RequiredLevel(UpgradeType.PumpSpeed, 0));
            Assert.AreEqual(4, ProgressMath.RequiredLevel(UpgradeType.ExtraPump, 0));
        }

        [Test]
        public void MarketPrice_StaysWithinBounds()
        {
            float price = 1.5f;
            var random = new Unity.Mathematics.Random(3);
            for (int day = 0; day < 500; day++)
            {
                price = ProgressMath.NextMarketPrice(price, 1.5f, random.NextFloat());
                Assert.GreaterOrEqual(price, 1.5f * 0.7f - 1e-4f);
                Assert.LessOrEqual(price, 1.5f * 1.5f + 1e-4f);
            }
        }

        [Test]
        public void BuyPrice_IsBelowMarket()
        {
            Assert.Less(ProgressMath.BuyPrice(1.5f), 1.5f);
        }

        [Test]
        public void CustomerPick_OnlyRegularAndHurryAtLevelOne()
        {
            for (float r = 0f; r < 1f; r += 0.01f)
            {
                var type = CustomerProfiles.Pick(r, 1, 12f, false);
                Assert.That(type == CustomerType.Regular || type == CustomerType.Hurry, $"got {type} at {r}");
            }
        }

        [Test]
        public void CustomerPick_UnlocksTypesWithLevel()
        {
            bool trucker = false, tourist = false, thief = false;
            for (float r = 0f; r < 1f; r += 0.002f)
            {
                var type = CustomerProfiles.Pick(r, 5, 23f, true);
                trucker |= type == CustomerType.Trucker;
                tourist |= type == CustomerType.Tourist;
                thief |= type == CustomerType.Thief;
            }

            Assert.IsTrue(trucker && tourist && thief);
        }

        [Test]
        public void Tip_DependsOnPatience()
        {
            Assert.AreEqual(0f, CustomerProfiles.Tip(CustomerType.Regular, 100f, 1f));
            Assert.Greater(CustomerProfiles.Tip(CustomerType.Hurry, 100f, 1f), CustomerProfiles.Tip(CustomerType.Hurry, 100f, 0.2f));
        }

        [Test]
        public void TruckerAlwaysTakesDiesel()
        {
            Assert.IsTrue(CustomerProfiles.Get(CustomerType.Trucker).DieselOnly);
        }
    }
}
