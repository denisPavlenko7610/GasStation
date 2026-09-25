using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class ShopTests
    {
        private static ShopProduct Shelf(ProductType type, int stock)
        {
            var defaults = ShopMath.Defaults(type);
            return new ShopProduct
            {
                Stock = stock,
                Capacity = defaults.Capacity,
                BuyPrice = defaults.BuyPrice,
                SellPrice = defaults.SellPrice,
                ReferencePrice = defaults.SellPrice
            };
        }

        [Test]
        public void Pick_ReturnsMinusOneWhenShopIsEmpty()
        {
            var empty = Shelf(ProductType.Water, 0);
            Assert.AreEqual(-1, ShopMath.Pick(0.5f, CustomerType.Regular, empty, empty, empty, empty, empty));
        }

        [Test]
        public void Pick_OnlyChoosesProductsInStock()
        {
            var empty = Shelf(ProductType.Water, 0);
            var coffee = Shelf(ProductType.Coffee, 5);
            for (float r = 0f; r < 1f; r += 0.05f)
                Assert.AreEqual((int)ProductType.Coffee, ShopMath.Pick(r, CustomerType.Regular, empty, coffee, empty, empty, empty));
        }

        [Test]
        public void Tourists_PreferSouvenirs()
        {
            var souvenir = Shelf(ProductType.Souvenir, 5);
            Assert.Greater(ShopMath.Weight(CustomerType.Tourist, ProductType.Souvenir, souvenir),
                ShopMath.Weight(CustomerType.Regular, ProductType.Souvenir, souvenir));
        }

        [Test]
        public void Overpriced_ProductsSellLess()
        {
            var fair = Shelf(ProductType.Snacks, 5);
            var expensive = fair;
            expensive.SellPrice *= 1.5f;
            Assert.Less(ShopMath.Weight(CustomerType.Regular, ProductType.Snacks, expensive),
                ShopMath.Weight(CustomerType.Regular, ProductType.Snacks, fair));
        }

        [Test]
        public void EveryProduct_HasMargin()
        {
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                var defaults = ShopMath.Defaults((ProductType)i);
                Assert.Greater(defaults.SellPrice, defaults.BuyPrice, ((ProductType)i).ToString());
                Assert.LessOrEqual(defaults.StartStock, defaults.Capacity);
            }
        }

        [Test]
        public void Wash_UpgradesAreFasterAndPricier()
        {
            Assert.Less(ShopMath.WashDuration(12f, 3), ShopMath.WashDuration(12f, 1));
            Assert.Greater(ShopMath.WashPrice(8f, 3), ShopMath.WashPrice(8f, 1));
            Assert.AreEqual(12f, ShopMath.WashDuration(12f, 1));
        }

        [Test]
        public void Thieves_NeverShopOrWash()
        {
            var thief = CustomerProfiles.Get(CustomerType.Thief);
            Assert.AreEqual(0f, thief.ShopChance);
            Assert.AreEqual(0f, thief.WashChance);
        }
    }
}
