using GasStation.Components;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class SaveDataTests
    {
        [Test]
        public void JsonRoundTrip_RestoresState()
        {
            var economy = new Economy { Money = 1234f, Reputation = 0.7f, DailyFixedCosts = 230f, DayServed = 5 };
            var time = new GameTime { Day = 4, Hour = 13.5f, MinutesPerSecond = 1f };
            var upgrades = new StationUpgrades { PumpSpeed = 2, Attendant = 1 };
            var stock = new[]
            {
                new FuelStock { Amount = 500f, Capacity = 3000f, SellPrice = 1.45f },
                new FuelStock { Amount = 10f, Capacity = 3000f, SellPrice = 1.6f },
                new FuelStock { Amount = 0f, Capacity = 3000f, SellPrice = 1.5f }
            };
            var pending = new[] { 0f, 0f, 400f };

            var saved = SaveData.Create(economy, time, upgrades, stock, pending);
            saved.CaptureQuest(new QuestProgress { Index = 3, Counter = 2f, Completed = 3 });
            saved.CaptureLevel(new StationLevel { Level = 4, Experience = 120f });
            string json = JsonUtility.ToJson(saved);
            var loaded = JsonUtility.FromJson<SaveData>(json);

            var restoredEconomy = new Economy();
            var restoredTime = new GameTime { MinutesPerSecond = 2f };
            var restoredUpgrades = new StationUpgrades();
            var restoredStock = new FuelStock[3];
            loaded.ApplyTo(ref restoredEconomy, ref restoredTime, ref restoredUpgrades, restoredStock);

            Assert.AreEqual(1234f, restoredEconomy.Money);
            Assert.AreEqual(230f, restoredEconomy.DailyFixedCosts);
            Assert.AreEqual(5, restoredEconomy.DayServed);
            Assert.AreEqual(4, restoredTime.Day);
            Assert.AreEqual(13.5f, restoredTime.Hour);
            Assert.AreEqual(2f, restoredTime.MinutesPerSecond, "Time scale comes from the scene, not the save");
            Assert.AreEqual(2, restoredUpgrades.PumpSpeed);
            Assert.AreEqual(1, restoredUpgrades.Attendant);
            Assert.AreEqual(1.6f, restoredStock[1].SellPrice);
            Assert.AreEqual(400f, restoredStock[2].Amount, "Paid deliveries are saved as delivered");
            Assert.AreEqual(3, loaded.ToQuestProgress().Index);
            Assert.AreEqual(2f, loaded.ToQuestProgress().Counter);
            Assert.AreEqual(4, loaded.ToStationLevel().Level);
            Assert.AreEqual(120f, loaded.ToStationLevel().Experience);
        }

        [Test]
        public void Version1Save_IsStillSupported()
        {
            var loaded = JsonUtility.FromJson<SaveData>("{\"version\":1,\"day\":2,\"money\":50}");
            Assert.IsTrue(loaded.IsSupported);
            Assert.IsNull(loaded.trash, "Old saves keep the scene litter");
            Assert.AreEqual(0, loaded.ToQuestProgress().Index);
            Assert.AreEqual(1, loaded.ToStationLevel().Level, "Old saves start at level 1");
            Assert.IsNull(loaded.pumps, "Old saves keep the scene pump condition");
            Assert.IsNull(loaded.products, "Old saves keep the scene shop stock");
            Assert.AreEqual(-1, loaded.paintScheme, "Old saves keep the scene paint");
            Assert.Less(loaded.restroomDirt, 0f, "Old saves keep the scene restroom");
        }
    }
}
