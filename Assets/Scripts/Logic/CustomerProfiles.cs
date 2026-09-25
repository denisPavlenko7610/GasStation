using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public struct CustomerProfile
    {
        public float Weight;
        public int MinStationLevel;
        public float LitersMultiplier;
        public float PatienceMultiplier;
        public float SpeedMultiplier;
        /// <summary>Tip as a share of the bill, scaled by how patient the customer still is.</summary>
        public float TipShare;
        public float LitterMultiplier;
        public bool DieselOnly;
        public float ShopChance;
        public float WashChance;
        public float TireChance;
    }

    public static class CustomerProfiles
    {
        public const int Count = 5;

        public static CustomerProfile Get(CustomerType type) => type switch
        {
            CustomerType.Trucker => new CustomerProfile
            {
                Weight = 12f, MinStationLevel = 2, LitersMultiplier = 3f, PatienceMultiplier = 1.5f,
                SpeedMultiplier = 0.7f, TipShare = 0.05f, LitterMultiplier = 1f, DieselOnly = true,
                ShopChance = 0.6f, WashChance = 0.1f, TireChance = 0.2f
            },
            CustomerType.Hurry => new CustomerProfile
            {
                Weight = 15f, MinStationLevel = 1, LitersMultiplier = 0.6f, PatienceMultiplier = 0.5f,
                SpeedMultiplier = 1.3f, TipShare = 0.15f, LitterMultiplier = 0.5f,
                ShopChance = 0.1f, WashChance = 0.05f, TireChance = 0.05f
            },
            CustomerType.Tourist => new CustomerProfile
            {
                Weight = 10f, MinStationLevel = 3, LitersMultiplier = 1f, PatienceMultiplier = 1.2f,
                SpeedMultiplier = 0.9f, TipShare = 0.05f, LitterMultiplier = 3f,
                ShopChance = 0.8f, WashChance = 0.3f, TireChance = 0.1f
            },
            CustomerType.Thief => new CustomerProfile
            {
                Weight = 3f, MinStationLevel = 2, LitersMultiplier = 1.5f, PatienceMultiplier = 1f,
                SpeedMultiplier = 1.2f, TipShare = 0f, LitterMultiplier = 1f,
                ShopChance = 0f, WashChance = 0f, TireChance = 0f
            },
            _ => new CustomerProfile
            {
                Weight = 60f, MinStationLevel = 1, LitersMultiplier = 1f, PatienceMultiplier = 1f,
                SpeedMultiplier = 1f, TipShare = 0f, LitterMultiplier = 1f,
                ShopChance = 0.35f, WashChance = 0.2f, TireChance = 0.1f
            }
        };

        /// <summary>Weighted pick among the types unlocked at this level. Thieves are three times likelier at night.</summary>
        public static CustomerType Pick(float random01, int stationLevel, float hour, bool touristBoost)
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
                total += Weight((CustomerType)i, stationLevel, hour, touristBoost);

            float roll = random01 * total;
            for (int i = 0; i < Count; i++)
            {
                roll -= Weight((CustomerType)i, stationLevel, hour, touristBoost);
                if (roll < 0f)
                    return (CustomerType)i;
            }

            return CustomerType.Regular;
        }

        public static float Tip(CustomerType type, float bill, float patienceRatio) =>
            bill * Get(type).TipShare * math.saturate(patienceRatio);

        private static float Weight(CustomerType type, int stationLevel, float hour, bool touristBoost)
        {
            var profile = Get(type);
            if (stationLevel < profile.MinStationLevel)
                return 0f;

            float weight = profile.Weight;
            bool night = hour < 5f || hour >= 22f;
            if (type == CustomerType.Thief && night)
                weight *= 3f;
            if (type == CustomerType.Tourist && touristBoost)
                weight *= 5f;
            return weight;
        }
    }
}
