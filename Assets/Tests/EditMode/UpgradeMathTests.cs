using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class UpgradeMathTests
    {
        [Test]
        public void Cost_GrowsWithLevel()
        {
            foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
                Assert.Greater(UpgradeMath.Cost(type, 1), UpgradeMath.Cost(type, 0));
        }

        [Test]
        public void CanUpgrade_StopsAtMaxLevel()
        {
            Assert.IsTrue(UpgradeMath.CanUpgrade(UpgradeType.PumpSpeed, 0));
            Assert.IsFalse(UpgradeMath.CanUpgrade(UpgradeType.PumpSpeed, UpgradeMath.MaxLevel(UpgradeType.PumpSpeed)));
        }

        [Test]
        public void LegacyStaffUpgrades_CannotBeBought()
        {
            Assert.IsFalse(UpgradeMath.CanUpgrade(UpgradeType.Attendant, 0));
            Assert.IsFalse(UpgradeMath.CanUpgrade(UpgradeType.Janitor, 0));
            Assert.IsFalse(UpgradeMath.CanUpgrade(UpgradeType.Mechanic, 0));
            CollectionAssert.DoesNotContain(UpgradeMath.Purchasable, UpgradeType.Attendant);
        }

        [Test]
        public void EveryPurchasableUpgrade_CanBeBought()
        {
            foreach (var type in UpgradeMath.Purchasable)
                Assert.IsTrue(UpgradeMath.CanUpgrade(type, 0), type.ToString());
        }

        [Test]
        public void StationUpgrades_GetReturnsWhatSetStored()
        {
            var upgrades = new StationUpgrades();
            for (int i = 0; i < UpgradeTypes.Count; i++)
                upgrades.Set((UpgradeType)i, i + 1);

            for (int i = 0; i < UpgradeTypes.Count; i++)
                Assert.AreEqual(i + 1, upgrades.Get((UpgradeType)i));
        }
    }
}
