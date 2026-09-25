using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>One regular: who they are, what they drive and when they come. Texts are regular.{index}.*.</summary>
    public struct RegularInfo
    {
        public CustomerType Type;
        public FuelType Fuel;
        /// <summary>Game hour they usually arrive; they come within the next two hours.</summary>
        public int Hour;
        /// <summary>Bit i = comes on weekday i (day 1 = weekday 0).</summary>
        public byte Days;
        public float Liters;
        public bool Shop;
        public bool Wash;
    }

    /// <summary>
    /// Twenty regulars of the desert highway. They come on their days, remember how they were treated and
    /// leave reviews under their own names. Managed data: read it from managed code (VisitorSystem, UI), not Burst.
    /// </summary>
    public static class RegularCatalog
    {
        public const byte EveryDay = 0x7F;
        public const byte Weekdays = 0x1F;
        public const byte Weekend = 0x60;

        private static byte D(params int[] days)
        {
            byte mask = 0;
            foreach (int day in days)
                mask |= (byte)(1 << day);
            return mask;
        }

        // 0 = Mon ... 6 = Sun (day 1 is a Monday).
        private static readonly RegularInfo[] All =
        {
            new() { Type = CustomerType.Trucker, Fuel = FuelType.Diesel, Hour = 6, Days = D(0, 2, 4), Liters = 1f, Shop = true },     // Bob
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol92, Hour = 8, Days = D(0, 1, 2, 3, 4, 5), Liters = 1.3f },     // Marina, taxi
            new() { Type = CustomerType.Tourist, Fuel = FuelType.Petrol95, Hour = 11, Days = Weekend, Liters = 1.2f, Shop = true, Wash = true }, // the Ivanovs
            new() { Type = CustomerType.Biker, Fuel = FuelType.Petrol95, Hour = 17, Days = D(4, 5), Liters = 1f, Shop = true },      // Harry
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol92, Hour = 9, Days = EveryDay, Liters = 0.6f, Shop = true },  // old Pete
            new() { Type = CustomerType.Hurry, Fuel = FuelType.Petrol92, Hour = 13, Days = Weekdays, Liters = 1f },                  // Lena, courier
            new() { Type = CustomerType.Trucker, Fuel = FuelType.Diesel, Hour = 22, Days = D(1, 3), Liters = 1.2f, Shop = true },    // Big Mike
            new() { Type = CustomerType.Hurry, Fuel = FuelType.Petrol95, Hour = 7, Days = Weekdays, Liters = 0.8f },                 // Dr. Carter
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol92, Hour = 15, Days = EveryDay, Liters = 1f },                // Sheriff Dawson
            new() { Type = CustomerType.Regular, Fuel = FuelType.Diesel, Hour = 10, Days = D(1, 5), Liters = 1.5f },                 // Rosa, farmer
            new() { Type = CustomerType.Hurry, Fuel = FuelType.Petrol95, Hour = 16, Days = D(0, 1, 2), Liters = 1f, Shop = true },   // Tony, salesman
            new() { Type = CustomerType.Tourist, Fuel = FuelType.Petrol95, Hour = 12, Days = D(6), Liters = 1f, Shop = true },       // the Kims
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol92, Hour = 19, Days = D(4), Liters = 0.5f, Shop = true },     // Jake, student
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol92, Hour = 11, Days = D(2), Liters = 0.7f, Shop = true },     // Grandma Olga
            new() { Type = CustomerType.Regular, Fuel = FuelType.Diesel, Hour = 8, Days = D(0, 3), Liters = 1f, Wash = true },       // Sam, mechanic
            new() { Type = CustomerType.Tourist, Fuel = FuelType.Petrol95, Hour = 18, Days = D(5), Liters = 1f, Wash = true },       // Nina, photographer
            new() { Type = CustomerType.Trucker, Fuel = FuelType.Diesel, Hour = 3, Days = D(2, 6), Liters = 1.4f },                  // Viktor
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol95, Hour = 14, Days = D(1, 4), Liters = 1f, Shop = true },    // Chef Luigi
            new() { Type = CustomerType.Tourist, Fuel = FuelType.Diesel, Hour = 20, Days = D(5), Liters = 2f, Shop = true },         // the Dust Devils band
            new() { Type = CustomerType.Regular, Fuel = FuelType.Petrol95, Hour = 10, Days = D(0), Liters = 1f, Wash = true },       // Mayor Brooks
        };

        public static int Count => All.Length;

        /// <param name="index">0-based.</param>
        public static RegularInfo Get(int index) => All[math.clamp(index, 0, All.Length - 1)];

        public static int Weekday(int day) => ((day - 1) % 7 + 7) % 7;

        public static bool ComesOn(RegularInfo info, int day) => (info.Days & (1 << Weekday(day))) != 0;

        public static bool InWindow(RegularInfo info, int hour) => ((hour - info.Hour) % 24 + 24) % 24 < 2;
    }

    /// <summary>Loyalty of regulars and the effects of special guests.</summary>
    public static class VisitorMath
    {
        public const int UpsetsToLeave = 3;
        public const float BestFriendLoyalty = 0.95f;
        public const float StartLoyalty = 0.3f;

        public const int CriticMinLevel = 3;
        public const int CriticEveryDays = 5;
        public const float CriticPraise = 1.1f;
        public const float CriticPan = 0.9f;
        public const int ArticleDays = 7;

        public const int BusMinLevel = 4;
        public const int BusEveryDays = 3;
        public const int BusMinPassengers = 10;
        public const int BusMaxPassengers = 16;
        public const int ConvoyMin = 3;
        public const int ConvoyMax = 6;
        public const float ConvoyInterval = 1.5f;

        // Hourly chances of special guests (VisitorSystem) and the hours they come.
        public const float CriticChancePerHour = 0.12f;
        public const float BusChancePerHour = 0.15f;
        public const float ConvoyChancePerHour = 0.05f;
        public const float EmergencyChancePerHour = 0.03f;
        public const float TowTruckChancePerHour = 0.05f;
        public const int BusFirstHour = 10;
        public const int BusLastHour = 16;
        public const int ConvoyFirstHour = 16;
        public const int ConvoyLastHour = 21;
        public const int DaytimeFirstHour = 10;
        public const int DaytimeLastHour = 18;
        /// <summary>Regulars arrive spread over this many seconds after their hour starts.</summary>
        public const float RegularArrivalSpread = 20f;
        public const float CriticArrivalSpread = 30f;

        public const float EmergencyReputation = 0.02f;
        public const float TowTireMultiplier = 2f;

        /// <summary>Loyalty after a visit: 5 stars +0.1, 3 stars nothing, 1 star -0.2.</summary>
        public static float NextLoyalty(float loyalty, float stars)
        {
            float delta = stars >= 3f ? (stars - 3f) * 0.05f : (stars - 3f) * 0.1f;
            return math.saturate(loyalty + delta);
        }

        public static bool IsUpset(float stars) => stars <= 2f;

        /// <summary>Chance per hour of their window that a regular turns up: 40% for a stranger, 90% for a friend.</summary>
        public static float VisitChance(float loyalty) => math.lerp(0.4f, 0.9f, math.saturate(loyalty));

        /// <summary>Loyal regulars tip: up to +10% of the bill.</summary>
        public static float TipShare(float loyalty) => math.max(0f, loyalty - 0.5f) * 0.2f;

        /// <summary>Every regular with loyalty over 0.8 brings friends: +2% traffic each.</summary>
        public static float FriendsTrafficFactor(int loyalRegulars) => 1f + 0.02f * loyalRegulars;

        /// <summary>The critic's article: praise for 5 stars, a pan for 2 or less, no article otherwise.</summary>
        public static float ArticleFactor(float stars) => stars >= 4.5f ? CriticPraise : stars <= 2f ? CriticPan : 1f;
    }
}
