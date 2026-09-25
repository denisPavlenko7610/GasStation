using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class SeasonTests
    {
        [Test]
        public void Seasons_FollowTheCalendar()
        {
            Assert.AreEqual(SeasonKind.Spring, SeasonMath.SeasonOf(1));
            Assert.AreEqual(SeasonKind.Spring, SeasonMath.SeasonOf(14));
            Assert.AreEqual(SeasonKind.Summer, SeasonMath.SeasonOf(15));
            Assert.AreEqual(SeasonKind.Winter, SeasonMath.SeasonOf(43));
            Assert.AreEqual(SeasonKind.Spring, SeasonMath.SeasonOf(57));
            Assert.AreEqual(1, SeasonMath.DayOfSeason(15));
        }

        [Test]
        public void Snow_OnlyInWinter_HeatOnlyInSummer()
        {
            for (float r = 0f; r < 1f; r += 0.02f)
            {
                Assert.AreNotEqual(WeatherKind.Snow, SeasonMath.RollWeather(SeasonKind.Summer, r));
                Assert.AreNotEqual(WeatherKind.Heat, SeasonMath.RollWeather(SeasonKind.Winter, r));
            }

            Assert.AreEqual(WeatherKind.Snow, SeasonMath.RollWeather(SeasonKind.Winter, 0.1f));
            Assert.AreEqual(WeatherKind.Heat, SeasonMath.RollWeather(SeasonKind.Summer, 0.1f));
        }

        [Test]
        public void Seasons_ShiftDemand()
        {
            Assert.Greater(SeasonMath.ProductFactor(ProductType.Water, SeasonKind.Summer, WeatherKind.Heat), 2f);
            Assert.Greater(SeasonMath.ProductFactor(ProductType.Coffee, SeasonKind.Winter, WeatherKind.Snow), 2f);
            Assert.AreEqual(1f, SeasonMath.ProductFactor(ProductType.MotorOil, SeasonKind.Summer, WeatherKind.Heat));
            Assert.Greater(SeasonMath.WashFactor(WeatherKind.Rain), 1f);
            Assert.Less(SeasonMath.TrafficFactor(SeasonKind.Winter, WeatherKind.Snow), 0.7f);
            Assert.AreEqual(CustomerType.Trucker, SeasonMath.AdjustCustomer(CustomerType.Tourist, SeasonKind.Winter, 0.1f));
            Assert.AreEqual(CustomerType.Tourist, SeasonMath.AdjustCustomer(CustomerType.Tourist, SeasonKind.Summer, 0.1f));
        }

        [Test]
        public void Plates_AreMostlyFromNearby()
        {
            int near = 0, far = 0;
            for (float r = 0f; r < 1f; r += 0.001f)
            {
                int plate = PlateMath.Pick(r);
                Assert.Less(plate, PlateMath.Count);
                if (plate < 4) near++;
                if (plate >= 16) far++;
            }

            Assert.Greater(near, far * 5);
            Assert.Greater(far, 0, "far plates must still be collectable");
            Assert.AreEqual(2, PlateMath.Collected(0b101));
            Assert.IsTrue(PlateMath.Has(0b100, 2));
        }

        [Test]
        public void EverySeasonWeatherAndPlate_IsLocalized()
        {
            foreach (SeasonKind season in Enum.GetValues(typeof(SeasonKind)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"season.{season}"));
                Assert.IsTrue(LocTable.Entries.ContainsKey($"season.{season}.start"));
            }

            foreach (WeatherKind weather in Enum.GetValues(typeof(WeatherKind)))
                Assert.IsTrue(LocTable.Entries.ContainsKey($"weather.{weather}"));
            for (int i = 0; i < PlateMath.Count; i++)
                Assert.IsTrue(LocTable.Entries.ContainsKey($"plate.{i}"), $"plate.{i}");
        }
    }
}
