using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;
using Unity.Mathematics;

namespace GasStation.Tests
{
    public class StationMathTests
    {
        [Test]
        public void TrafficIntensity_RushHourIsBusierThanNight()
        {
            Assert.Greater(StationMath.TrafficIntensity(8.5f), StationMath.TrafficIntensity(3f));
            Assert.Greater(StationMath.TrafficIntensity(18f), StationMath.TrafficIntensity(23f));
        }

        [Test]
        public void TrafficIntensity_IsAlwaysPositive()
        {
            for (float hour = 0f; hour < 24f; hour += 0.5f)
                Assert.Greater(StationMath.TrafficIntensity(hour), 0f);
        }

        [Test]
        public void PriceAttractiveness_MarketPriceIsNeutral()
        {
            Assert.AreEqual(1f, StationMath.PriceAttractiveness(1.5f, 1.5f), 1e-5f);
        }

        [Test]
        public void PriceAttractiveness_CheaperAttractsMore_AndIsClamped()
        {
            Assert.Greater(StationMath.PriceAttractiveness(1.3f, 1.5f), 1f);
            Assert.Less(StationMath.PriceAttractiveness(1.7f, 1.5f), 1f);
            Assert.AreEqual(0.1f, StationMath.PriceAttractiveness(100f, 1.5f), 1e-5f);
            Assert.AreEqual(2f, StationMath.PriceAttractiveness(0f, 1.5f), 1e-5f);
        }

        [TestCase(10f, 50f, 100f, ExpectedResult = 10f)]
        [TestCase(10f, 3f, 100f, ExpectedResult = 3f)]
        [TestCase(10f, 50f, 2f, ExpectedResult = 2f)]
        [TestCase(10f, 50f, 0f, ExpectedResult = 0f)]
        public float Dispense_IsLimitedByFlowRequestAndStock(float flow, float requested, float stock)
        {
            return StationMath.Dispense(flow, requested, stock);
        }

        [Test]
        public void AdvanceClock_RollsOverToNextDay()
        {
            float hour = 23.5f;
            int day = 1;

            Assert.IsFalse(StationMath.AdvanceClock(ref hour, ref day, 0.25f));
            Assert.IsTrue(StationMath.AdvanceClock(ref hour, ref day, 0.5f));
            Assert.AreEqual(2, day);
            Assert.AreEqual(0.25f, hour, 1e-4f);
        }

        [Test]
        public void QueueSlot_IsSpacedAlongDirection()
        {
            var slot = StationMath.QueueSlot(new float3(1f, 0f, 0f), new float3(-1f, 0f, 0f), 6f, 2);
            Assert.AreEqual(-11f, slot.x, 1e-5f);
        }

        [Test]
        public void PickFuelType_CoversAllTypes()
        {
            Assert.AreEqual(FuelType.Petrol92, StationMath.PickFuelType(0.1f));
            Assert.AreEqual(FuelType.Petrol95, StationMath.PickFuelType(0.6f));
            Assert.AreEqual(FuelType.Diesel, StationMath.PickFuelType(0.95f));
        }

        [Test]
        public void ServiceReputationDelta_OverpricingHurts()
        {
            float fair = StationMath.ServiceReputationDelta(1f, 1.5f, 1.5f);
            float greedy = StationMath.ServiceReputationDelta(1f, 2.25f, 1.5f);
            Assert.Greater(fair, 0f);
            Assert.Less(greedy, 0f);
        }
    }
}
