using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class ShelfAndTheftTests
    {
        [Test]
        public void OnlyPerishables_Expire()
        {
            Assert.IsFalse(ShopMath.Expired(ProductType.Water, 100f));
            Assert.IsFalse(ShopMath.Expired(ProductType.Snacks, 2f));
            Assert.IsTrue(ShopMath.Expired(ProductType.Snacks, 3f));
            Assert.AreEqual(0, ShopMath.Spoiled(ProductType.MotorOil, 10, 50f));
            Assert.AreEqual(5, ShopMath.Spoiled(ProductType.Snacks, 9, 3f));
        }

        [Test]
        public void FreshDelivery_LowersTheAge()
        {
            Assert.AreEqual(2f, ShopMath.AgeAfterDelivery(4f, 10, 10), 0.001f);
            Assert.AreEqual(0f, ShopMath.AgeAfterDelivery(4f, 0, 10), 0.001f);
            Assert.AreEqual(0f, ShopMath.AgeAfterDelivery(4f, 0, 0), 0.001f);
        }

        [Test]
        public void Suppliers_TradePriceForSpeed()
        {
            Assert.Less(ShopMath.SupplierPriceFactor(SupplierKind.Cheap), ShopMath.SupplierPriceFactor(SupplierKind.Reliable));
            Assert.Greater(ShopMath.SupplierTimeFactor(SupplierKind.Cheap), ShopMath.SupplierTimeFactor(SupplierKind.Reliable));
        }

        [Test]
        public void Promo_SellsTwoForOne_AndIsMoreWanted()
        {
            var shelf = new ShopProduct { Stock = 5, SellPrice = 2f, ReferencePrice = 2f };
            var promo = shelf;
            promo.Promo = true;
            Assert.AreEqual(1, ShopMath.UnitsPerSale(shelf));
            Assert.AreEqual(2, ShopMath.UnitsPerSale(promo));
            promo.Stock = 1;
            Assert.AreEqual(1, ShopMath.UnitsPerSale(promo));
            promo.Stock = 5;
            Assert.Greater(ShopMath.Weight(CustomerType.Regular, ProductType.Snacks, promo),
                ShopMath.Weight(CustomerType.Regular, ProductType.Snacks, shelf));
        }

        [Test]
        public void Shoplifters_AreCaughtByStaffCamerasAndThePlayer()
        {
            Assert.AreEqual(0f, ShopMath.ShopliftCatchChance(0f, 0, false));
            Assert.AreEqual(1f, ShopMath.ShopliftCatchChance(0f, 0, true));
            Assert.Greater(ShopMath.ShopliftCatchChance(1f, 1, false), ShopMath.ShopliftCatchChance(1f, 0, false));
            Assert.LessOrEqual(ShopMath.ShopliftCatchChance(10f, 10, false), 0.9f);
            Assert.Greater(ShopMath.ShopliftChance(CustomerType.Thief), ShopMath.ShopliftChance(CustomerType.Regular));
        }

        [Test]
        public void Robbery_IsCapped_AndCanBePrevented()
        {
            Assert.AreEqual(0f, ShopMath.RobberyLoss(-100f));
            Assert.AreEqual(200f, ShopMath.RobberyLoss(1000f));
            Assert.AreEqual(ShopMath.RobberyMax, ShopMath.RobberyLoss(1e6f));
            Assert.AreEqual(0f, ShopMath.RobberyPreventChance(0, 0, 0));
            Assert.LessOrEqual(ShopMath.RobberyPreventChance(10, 10, 10), 0.9f);
        }

        [Test]
        public void Suppliers_AreLocalized()
        {
            foreach (var supplier in new[] { SupplierKind.Cheap, SupplierKind.Reliable })
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"supplier.{supplier}"));
                Assert.IsTrue(LocTable.Entries.ContainsKey($"supplier.{supplier}.desc"));
            }
        }
    }
}
