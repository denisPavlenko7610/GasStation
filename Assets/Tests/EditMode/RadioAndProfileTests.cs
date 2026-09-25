using GasStation.Bridge;
using GasStation.Logic;
using GasStation.Mono.Audio;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class RadioAndProfileTests
    {
        [TestCase(RadioStation.Country)]
        [TestCase(RadioStation.Synthwave)]
        [TestCase(RadioStation.LoFi)]
        public void EveryStation_GeneratesAnAudibleLoop(RadioStation station)
        {
            var clip = MusicSynth.Create(station);
            try
            {
                Assert.Greater(clip.length, 10f);
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);

                float peak = 0f;
                foreach (float sample in samples)
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                Assert.That(peak > 0.1f && peak <= 1f, $"peak {peak}");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void StationName_IsTrimmedAndLimited()
        {
            StationProfile.SetName("   " + new string('A', 40) + "   ");
            Assert.AreEqual(StationProfile.MaxNameLength, StationProfile.CustomName.Length);

            StationProfile.SetName("  ");
            Assert.IsNull(StationProfile.CustomName, "an empty name falls back to the default");
            Assert.IsFalse(string.IsNullOrEmpty(StationProfile.DisplayName));
            StationProfile.SetName(null);
        }

        [Test]
        public void Decor_AddsTraffic()
        {
            Assert.AreEqual(1f, UpgradeMath.DecorMultiplier(0));
            Assert.Greater(UpgradeMath.DecorMultiplier(3), UpgradeMath.DecorMultiplier(1));
        }
    }
}
