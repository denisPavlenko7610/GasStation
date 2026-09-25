using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>EV charging: few dollars from the electricity, more from the shop and the diner meanwhile.</summary>
    public static class EvMath
    {
        public const float PricePerKwh = 0.35f;
        public const float MinKwh = 20f;
        public const float MaxKwh = 50f;
        public const float MinMinutes = 20f;
        public const float MaxMinutes = 40f;
        /// <summary>Share of ordinary traffic that drives electric once chargers exist.</summary>
        public const float Share = 0.15f;
        public const int ChargersPerLevel = 2;
        /// <summary>Electricity the station buys per kWh sold (added to the night's bill).</summary>
        public const float CostPerKwh = 0.15f;

        public static int OpenChargers(int level, int spots) => math.min(spots, level * ChargersPerLevel);

        /// <summary>Seconds of real time for a charge of the given game minutes.</summary>
        public static float ChargeSeconds(float gameMinutes, float minutesPerSecond) =>
            minutesPerSecond > 0f ? gameMinutes / minutesPerSecond : gameMinutes;

        public static float Bill(float kwh, float pricePerKwh) => math.round(kwh * pricePerKwh * 100f) / 100f;
    }

    /// <summary>The diner: dishes, cooking, hunger and cold food.</summary>
    public static class DinerMath
    {
        public const int IngredientOrder = 20;
        public const float IngredientPrice = 1.2f;
        public const float FreshHours = 3f;
        /// <summary>Seconds of work to cook one dish at efficiency 1.</summary>
        public const float CookSeconds = 6f;
        public const float PlayerCookBonus = 1f;

        public static float Price(DinerDish dish) => dish == DinerDish.Burger ? 6f : 3.5f;

        /// <summary>Burgers come with the second diner level.</summary>
        public static bool OnMenu(DinerDish dish, int level) => level >= (dish == DinerDish.Burger ? 2 : 1);

        public static int CounterCapacity(int level) => 4 + 4 * level;

        public static int IngredientCapacity(int level) => 20 + 20 * level;

        /// <summary>The third level cooks faster.</summary>
        public static float CookTime(int level) => level >= 3 ? CookSeconds * 0.7f : CookSeconds;

        /// <summary>Chance a shop visitor also buys food: truckers, tourists and bikers are hungrier, mealtimes more so.</summary>
        public static float HungerChance(CustomerType customer, float hour)
        {
            float chance = customer switch
            {
                CustomerType.Trucker => 0.6f,
                CustomerType.Tourist or CustomerType.TourBus or CustomerType.Biker => 0.5f,
                CustomerType.Electric => 0.55f,
                CustomerType.Hurry => 0.15f,
                _ => 0.3f
            };
            bool mealtime = (hour >= 6f && hour < 10f) || (hour >= 17f && hour < 21f);
            return math.min(0.95f, chance * (mealtime ? 1.5f : 1f));
        }

        /// <summary>Which dish to cook next: the one with less on the counter (burgers only when on the menu).</summary>
        public static int NextDish(int hotDogs, int burgers, int level, int capacity)
        {
            bool burgerOk = OnMenu(DinerDish.Burger, level) && burgers < capacity;
            bool hotDogOk = hotDogs < capacity;
            if (burgerOk && (!hotDogOk || burgers < hotDogs))
                return (int)DinerDish.Burger;
            return hotDogOk ? (int)DinerDish.HotDog : -1;
        }

        /// <summary>What a hungry customer takes: a burger if there is one and they can afford the time, else a hot dog.</summary>
        public static int Choose(int hotDogs, int burgers, float random01)
        {
            if (burgers > 0 && (hotDogs == 0 || random01 < 0.5f))
                return (int)DinerDish.Burger;
            return hotDogs > 0 ? (int)DinerDish.HotDog : -1;
        }
    }
}
