using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class ContractTests
    {
        [Test]
        public void EveryContract_HasTextsAndSaneTerms()
        {
            foreach (ContractType type in Enum.GetValues(typeof(ContractType)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"contract.{type}.name"), type.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"contract.{type}.desc"), type.ToString());
                var info = ContractMath.Get(type);
                Assert.Greater(info.Days, 0);
                Assert.Greater(info.Bonus, 0f);
                Assert.Greater(info.Penalty, 0f);
                Assert.Greater(ContractMath.VehiclesPerDay(type), 0);
                Assert.LessOrEqual(info.PriceShare, 1f, "contracts never pay above the market");
            }

            Assert.AreEqual(Enum.GetValues(typeof(ContractType)).Length, ContractTypes.Count);
        }

        [Test]
        public void Schedule_MatchesTheSlots()
        {
            Assert.IsTrue(ContractMath.IsSlot(ContractType.BusCompany, 8));
            Assert.IsFalse(ContractMath.IsSlot(ContractType.BusCompany, 9));
            Assert.IsTrue(ContractMath.IsSlot(ContractType.Delivery, 9));
            Assert.IsFalse(ContractMath.IsSlot(ContractType.Delivery, 10));
            Assert.IsTrue(ContractMath.IsSlot(ContractType.Delivery, 17));
            Assert.IsFalse(ContractMath.IsSlot(ContractType.Delivery, 19));
            Assert.IsTrue(ContractMath.IsSlot(ContractType.TaxiFleet, 7));
            Assert.IsTrue(ContractMath.IsSlot(ContractType.TaxiFleet, 20));
            Assert.AreEqual(3, ContractMath.VehiclesPerDay(ContractType.BusCompany));
            Assert.AreEqual(5, ContractMath.VehiclesPerDay(ContractType.Delivery));
            Assert.AreEqual(4, ContractMath.VehiclesPerDay(ContractType.TaxiFleet));
        }

        [Test]
        public void Offers_OnlyForUnlockedTypes()
        {
            for (float r = 0f; r < 1f; r += 0.05f)
            {
                int type = ContractMath.PickType(r, 1);
                Assert.AreEqual((int)ContractType.TaxiFleet, type);
            }

            bool bus = false;
            for (float r = 0f; r < 1f; r += 0.05f)
                bus |= ContractMath.PickType(r, 10) == (int)ContractType.BusCompany;
            Assert.IsTrue(bus);
            Assert.AreEqual(-1, ContractMath.PickType(0.5f, 0));
        }

        [Test]
        public void Price_IsRoundedToCents()
        {
            Assert.AreEqual(1.19f, ContractMath.OfferPrice(ContractType.Police, 1.4f), 0.001f);
        }

        [Test]
        public void ThreeMisses_BreakTheContract()
        {
            Assert.IsFalse(ContractMath.IsBroken(2));
            Assert.IsTrue(ContractMath.IsBroken(3));
            Assert.AreEqual(ContractMath.Get(ContractType.Delivery).Penalty * 3f, ContractMath.CancelPenalty(ContractType.Delivery));
        }

        [Test]
        public void ContractSave_RoundTrips()
        {
            var contract = new Contract { Id = 4, Type = ContractType.Police, Price = 1.2f, DaysLeft = 6, Served = 3, Missed = 1 };
            var json = JsonUtility.ToJson(ContractSaveData.From(contract));
            var restored = JsonUtility.FromJson<ContractSaveData>(json).ToContract(12, 9);
            Assert.AreEqual(4, restored.Id);
            Assert.AreEqual(ContractType.Police, restored.Type);
            Assert.AreEqual(6, restored.DaysLeft);
            Assert.AreEqual(1, restored.Missed);
            Assert.AreEqual(12, restored.LastSentDay);

            var offer = new ContractOffer { Id = 5, Type = ContractType.Delivery, Price = 1.4f, Days = 5, ExpiresIn = 2 };
            var restoredOffer = JsonUtility.FromJson<ContractSaveData>(JsonUtility.ToJson(ContractSaveData.From(offer))).ToOffer();
            Assert.AreEqual(ContractType.Delivery, restoredOffer.Type);
            Assert.AreEqual(2, restoredOffer.ExpiresIn);
        }
    }
}
