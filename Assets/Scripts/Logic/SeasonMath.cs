using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Seasons (two game weeks each) and daily weather: traffic, customers, demand and the car wash.</summary>
    public static class SeasonMath
    {
        public const int DaysPerSeason = 14;

        public static SeasonKind SeasonOf(int day) => (SeasonKind)(((math.max(1, day) - 1) / DaysPerSeason) % 4);

        public static int DayOfSeason(int day) => (math.max(1, day) - 1) % DaysPerSeason + 1;

        /// <summary>Today's weather from one roll: each season has its own mix.</summary>
        public static WeatherKind RollWeather(SeasonKind season, float random01) => season switch
        {
            SeasonKind.Summer => random01 < 0.4f ? WeatherKind.Heat : random01 < 0.5f ? WeatherKind.Rain : WeatherKind.Clear,
            SeasonKind.Autumn => random01 < 0.4f ? WeatherKind.Rain : WeatherKind.Clear,
            SeasonKind.Winter => random01 < 0.35f ? WeatherKind.Snow : random01 < 0.45f ? WeatherKind.Rain : WeatherKind.Clear,
            _ => random01 < 0.3f ? WeatherKind.Rain : WeatherKind.Clear
        };

        public static float TrafficFactor(SeasonKind season, WeatherKind weather)
        {
            float seasonFactor = season switch
            {
                SeasonKind.Summer => 1.1f,
                SeasonKind.Winter => 0.85f,
                _ => 1f
            };
            float weatherFactor = weather switch
            {
                WeatherKind.Snow => 0.7f,
                WeatherKind.Rain => 0.9f,
                _ => 1f
            };
            return seasonFactor * weatherFactor;
        }

        /// <summary>Summer brings tourists; in winter most tourists stay home and more trucks are on the road.</summary>
        public static CustomerType AdjustCustomer(CustomerType customer, SeasonKind season, float random01)
        {
            if (season == SeasonKind.Summer && customer == CustomerType.Regular && random01 < 0.15f)
                return CustomerType.Tourist;
            if (season == SeasonKind.Winter && customer == CustomerType.Tourist && random01 < 0.6f)
                return CustomerType.Trucker;
            return customer;
        }

        /// <summary>Water sells in the heat, coffee in the cold, souvenirs in summer.</summary>
        public static float ProductFactor(ProductType product, SeasonKind season, WeatherKind weather)
        {
            switch (product)
            {
                case ProductType.Water:
                    return (season == SeasonKind.Summer ? 1.8f : 1f) * (weather == WeatherKind.Heat ? 1.5f : 1f);
                case ProductType.Coffee:
                    return (season == SeasonKind.Winter ? 2f : season == SeasonKind.Autumn ? 1.3f : 1f) *
                           (weather == WeatherKind.Snow ? 1.3f : 1f);
                case ProductType.Souvenir:
                    return season == SeasonKind.Summer ? 1.5f : season == SeasonKind.Winter ? 0.6f : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>Rain and snow make cars dirty: the wash is wanted more.</summary>
        public static float WashFactor(WeatherKind weather) => weather switch
        {
            WeatherKind.Rain => 2f,
            WeatherKind.Snow => 1.5f,
            _ => 1f
        };
    }

    /// <summary>Where customers come from: nearby states are common, far ones rare.</summary>
    public static class PlateMath
    {
        public const int Count = 20;

        /// <summary>Relative frequency of each state's plates (index = plate id).</summary>
        private static float Weight(int plate) => plate switch
        {
            < 4 => 10f,   // neighbours
            < 10 => 4f,
            < 16 => 1.5f,
            _ => 0.4f     // the far corners of the country
        };

        public static byte Pick(float random01)
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
                total += Weight(i);

            float roll = random01 * total;
            for (int i = 0; i < Count; i++)
            {
                roll -= Weight(i);
                if (roll < 0f)
                    return (byte)i;
            }

            return 0;
        }

        public static bool Has(int seen, int plate) => (seen & (1 << plate)) != 0;

        public static int Collected(int seen)
        {
            int count = 0;
            for (int i = 0; i < Count; i++)
                count += (seen >> i) & 1;
            return count;
        }
    }
}
