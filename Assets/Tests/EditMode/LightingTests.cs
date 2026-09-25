using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class LightingTests
    {
        [TestCase(12f, ExpectedResult = 0f)]
        [TestCase(23f, ExpectedResult = 1f)]
        [TestCase(2f, ExpectedResult = 1f)]
        [TestCase(8f, ExpectedResult = 0f)]
        public float NightFactor_DayAndNight(float hour) => LightingMath.NightFactor(hour);

        [Test]
        public void NightFactor_FadesAtDuskAndDawn()
        {
            float dusk = LightingMath.NightFactor(19f);
            float dawn = LightingMath.NightFactor(6f);
            Assert.That(dusk > 0f && dusk < 1f);
            Assert.That(dawn > 0f && dawn < 1f);
            Assert.Less(LightingMath.NightFactor(18.7f), LightingMath.NightFactor(19.3f));
        }

        [Test]
        public void NightFactor_WrapsAroundMidnight()
        {
            Assert.AreEqual(LightingMath.NightFactor(1f), LightingMath.NightFactor(25f));
        }
    }
}
