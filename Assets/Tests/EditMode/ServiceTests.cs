using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class ServiceTests
    {
        [Test]
        public void Chargers_OpenTwoPerLevel()
        {
            Assert.AreEqual(0, EvMath.OpenChargers(0, 4));
            Assert.AreEqual(2, EvMath.OpenChargers(1, 4));
            Assert.AreEqual(4, EvMath.OpenChargers(2, 4));
            Assert.AreEqual(3, EvMath.OpenChargers(2, 3));
            Assert.AreEqual(2, UpgradeMath.MaxLevel(UpgradeType.EvCharger));
        }

        [Test]
        public void EvCharging_TakesGameMinutes_AndPaysLittle()
        {
            Assert.AreEqual(30f, EvMath.ChargeSeconds(30f, 1f), 0.001f);
            Assert.AreEqual(15f, EvMath.ChargeSeconds(30f, 2f), 0.001f);
            Assert.AreEqual(10.5f, EvMath.Bill(30f, 0.35f), 0.001f);
            Assert.Less(EvMath.CostPerKwh, EvMath.PricePerKwh);
        }

        [Test]
        public void EvLicence_ComesWithLevelFive()
        {
            Assert.AreEqual(5, ProgressMath.RequiredLevel(UpgradeType.EvCharger, 0));
            Assert.AreEqual(4, ProgressMath.RequiredLevel(UpgradeType.Diner, 0));
            CollectionAssert.Contains(UpgradeMath.Purchasable, UpgradeType.EvCharger);
            CollectionAssert.Contains(UpgradeMath.Purchasable, UpgradeType.Diner);
        }

        [Test]
        public void Burgers_ComeWithTheSecondLevel()
        {
            Assert.IsTrue(DinerMath.OnMenu(DinerDish.HotDog, 1));
            Assert.IsFalse(DinerMath.OnMenu(DinerDish.Burger, 1));
            Assert.IsTrue(DinerMath.OnMenu(DinerDish.Burger, 2));
            Assert.Less(DinerMath.CookTime(3), DinerMath.CookTime(1));
            Assert.Greater(DinerMath.Price(DinerDish.Burger), DinerMath.Price(DinerDish.HotDog));
        }

        [Test]
        public void Cook_FillsTheEmptierDish()
        {
            Assert.AreEqual((int)DinerDish.HotDog, DinerMath.NextDish(0, 0, 1, 8));
            Assert.AreEqual((int)DinerDish.Burger, DinerMath.NextDish(3, 1, 2, 8));
            Assert.AreEqual((int)DinerDish.HotDog, DinerMath.NextDish(1, 3, 2, 8));
            Assert.AreEqual(-1, DinerMath.NextDish(8, 8, 2, 8));
            Assert.AreEqual(-1, DinerMath.NextDish(8, 0, 1, 8));
        }

        [Test]
        public void Hungry_AtMealtimes()
        {
            Assert.Greater(DinerMath.HungerChance(CustomerType.Trucker, 8f), DinerMath.HungerChance(CustomerType.Trucker, 14f));
            Assert.Greater(DinerMath.HungerChance(CustomerType.Trucker, 14f), DinerMath.HungerChance(CustomerType.Hurry, 14f));
            Assert.LessOrEqual(DinerMath.HungerChance(CustomerType.Trucker, 8f), 0.95f);
            Assert.AreEqual(-1, DinerMath.Choose(0, 0, 0.5f));
            Assert.AreEqual((int)DinerDish.HotDog, DinerMath.Choose(2, 0, 0.1f));
        }

        [Test]
        public void EveryDish_IsLocalized()
        {
            Assert.IsTrue(LocTable.Entries.ContainsKey("dish.HotDog"));
            Assert.IsTrue(LocTable.Entries.ContainsKey("dish.Burger"));
            Assert.IsTrue(LocTable.Entries.ContainsKey("duty.Cook"));
        }
    }
}
