using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public struct ProductDefaults
    {
        public float BuyPrice;
        public float SellPrice;
        public int Capacity;
        public int StartStock;
        public float Demand;
    }

    public static class ShopMath
    {
        public const int OrderSize = 20;

        public static ProductDefaults Defaults(ProductType type) => type switch
        {
            ProductType.Water => new ProductDefaults { BuyPrice = 0.4f, SellPrice = 1.5f, Capacity = 40, StartStock = 15, Demand = 5f },
            ProductType.Coffee => new ProductDefaults { BuyPrice = 0.5f, SellPrice = 2.5f, Capacity = 40, StartStock = 15, Demand = 4f },
            ProductType.Snacks => new ProductDefaults { BuyPrice = 0.8f, SellPrice = 2.5f, Capacity = 40, StartStock = 15, Demand = 4f },
            ProductType.MotorOil => new ProductDefaults { BuyPrice = 6f, SellPrice = 12f, Capacity = 20, StartStock = 5, Demand = 1f },
            ProductType.Souvenir => new ProductDefaults { BuyPrice = 3f, SellPrice = 9f, Capacity = 20, StartStock = 5, Demand = 1f },
            _ => new ProductDefaults { BuyPrice = 1f, SellPrice = 2f, Capacity = 20, StartStock = 10, Demand = 1f }
        };

        /// <summary>How much a customer type wants a product, relative to the base demand.</summary>
        public static float Preference(CustomerType customer, ProductType product)
        {
            switch (customer)
            {
                case CustomerType.Trucker:
                    return product == ProductType.Coffee ? 3f : product == ProductType.MotorOil ? 2f : 1f;
                case CustomerType.Tourist:
                    return product == ProductType.Souvenir ? 4f : product == ProductType.Water ? 1.5f : 1f;
                case CustomerType.Hurry:
                    return product == ProductType.Coffee ? 2f : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>Weight of a product in the customer's choice: demand × preference × price appeal; 0 when out of stock.</summary>
        public static float Weight(CustomerType customer, ProductType product, ShopProduct shelf)
        {
            if (shelf.Stock <= 0)
                return 0f;
            return Defaults(product).Demand * Preference(customer, product)
                                            * StationMath.PriceAttractiveness(shelf.SellPrice, shelf.ReferencePrice);
        }

        /// <summary>Weighted random pick. Returns -1 when nothing can be bought.</summary>
        public static int Pick(float random01, CustomerType customer, ShopProduct p0, ShopProduct p1, ShopProduct p2,
            ShopProduct p3, ShopProduct p4)
        {
            float w0 = Weight(customer, ProductType.Water, p0);
            float w1 = Weight(customer, ProductType.Coffee, p1);
            float w2 = Weight(customer, ProductType.Snacks, p2);
            float w3 = Weight(customer, ProductType.MotorOil, p3);
            float w4 = Weight(customer, ProductType.Souvenir, p4);
            float total = w0 + w1 + w2 + w3 + w4;
            if (total <= 0f)
                return -1;

            float roll = random01 * total;
            if ((roll -= w0) < 0f) return 0;
            if ((roll -= w1) < 0f) return 1;
            if ((roll -= w2) < 0f) return 2;
            if ((roll -= w3) < 0f) return 3;
            return 4;
        }

        public static float WashDuration(float baseDuration, int level) => baseDuration / (1f + 0.5f * math.max(0, level - 1));

        /// <summary>Tire service levels: faster work, higher price (same curve as the wash).</summary>
        public static float TireDuration(float baseDuration, int level) => WashDuration(baseDuration, level);

        public static float TirePrice(float basePrice, int level) => WashPrice(basePrice, level);

        public static float WashPrice(float basePrice, int level) => basePrice * (1f + 0.25f * math.max(0, level - 1));
    }
}
