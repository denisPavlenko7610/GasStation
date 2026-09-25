using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class FinanceTests
    {
        [Test]
        public void Loans_CostMoreThanTheyGive_AndPayOffInTime()
        {
            foreach (var kind in new[] { LoanKind.Small, LoanKind.Large })
            {
                Assert.Greater(FinanceMath.LoanTotal(kind), FinanceMath.LoanAmount(kind));
                Assert.GreaterOrEqual(FinanceMath.LoanWeeklyPayment(kind) * FinanceMath.LoanWeeks(kind), FinanceMath.LoanTotal(kind));
            }

            Assert.AreEqual(5500f, FinanceMath.LoanTotal(LoanKind.Small), 0.01f);
            Assert.AreEqual(1375f, FinanceMath.LoanWeeklyPayment(LoanKind.Small));
        }

        [Test]
        public void EarlyRepayment_SavesSomeInterest_ButNeverTheLoanItself()
        {
            float balance = FinanceMath.LoanTotal(LoanKind.Large);
            float early = FinanceMath.EarlyRepayment(balance, LoanKind.Large);
            Assert.Less(early, balance);
            Assert.Greater(early, FinanceMath.LoanAmount(LoanKind.Large));
        }

        [Test]
        public void WeekStarts_OnDays8_15_22()
        {
            Assert.IsFalse(FinanceMath.IsWeekStart(1));
            Assert.IsFalse(FinanceMath.IsWeekStart(7));
            Assert.IsTrue(FinanceMath.IsWeekStart(8));
            Assert.IsTrue(FinanceMath.IsWeekStart(15));
            Assert.IsFalse(FinanceMath.IsWeekStart(16));
        }

        [Test]
        public void Tax_IsTenPercent_AndNeverNegative()
        {
            Assert.AreEqual(100f, FinanceMath.WeeklyTax(1000f));
            Assert.AreEqual(0f, FinanceMath.WeeklyTax(-500f));
        }

        [Test]
        public void Difficulty_ScalesBills_AndBankruptcy()
        {
            Assert.Less(FinanceMath.BillMultiplier(Difficulty.Relaxed), FinanceMath.BillMultiplier(Difficulty.Normal));
            Assert.Greater(FinanceMath.BillMultiplier(Difficulty.Survival), FinanceMath.BillMultiplier(Difficulty.Normal));
            Assert.AreEqual(0, FinanceMath.BankruptcyDays(Difficulty.Relaxed));
            Assert.Less(FinanceMath.BankruptcyDays(Difficulty.Survival), FinanceMath.BankruptcyDays(Difficulty.Normal));
            Assert.Greater(FinanceMath.BankruptcyDays(Difficulty.Survival), FinanceMath.BankruptcyWarningDays);
        }

        [Test]
        public void Electricity_GrowsWithPumps()
        {
            Assert.Greater(FinanceMath.DailyElectricity(4), FinanceMath.DailyElectricity(2));
        }

        [Test]
        public void Share_IsEven_ForEqualStations()
        {
            float appeal = CompetitionMath.Appeal(1.4f, 1.4f, 0.5f, 1f);
            Assert.AreEqual(0.5f, CompetitionMath.Share(appeal, appeal), 0.001f);
            Assert.AreEqual(1f, CompetitionMath.TrafficFactor(0.5f), 0.001f);
        }

        [Test]
        public void CheaperAndCleaner_WinsMoreDrivers()
        {
            float theirs = CompetitionMath.Appeal(1.4f, 1.4f, 0.5f, 1f);
            float cheaper = CompetitionMath.Appeal(1.3f, 1.4f, 0.5f, 1f);
            float dirty = CompetitionMath.Appeal(1.4f, 1.4f, 0.5f, CompetitionMath.OurExtras(0.6f, 0, 1f));
            Assert.Greater(CompetitionMath.Share(cheaper, theirs), 0.5f);
            Assert.Less(CompetitionMath.Share(dirty, theirs), 0.5f);
        }

        [Test]
        public void TrafficFactor_IsClamped()
        {
            Assert.AreEqual(0.3f, CompetitionMath.TrafficFactor(0f), 0.001f);
            Assert.AreEqual(1.6f, CompetitionMath.TrafficFactor(1f), 0.001f);
        }

        [Test]
        public void Competitor_FightsBack_WhenLosing()
        {
            Assert.AreNotEqual(CompetitorMove.Hold, CompetitionMath.DecideMove(0.8f, 0.9f));
            Assert.AreNotEqual(CompetitorMove.RaisePrices, CompetitionMath.DecideMove(0.8f, 0.1f));
            Assert.AreEqual(CompetitorMove.RaisePrices, CompetitionMath.DecideMove(0.2f, 0.5f));
            Assert.AreEqual(CompetitorMove.Hold, CompetitionMath.DecideMove(0.45f, 0.5f));
        }

        [Test]
        public void CompetitorPrice_StaysNearTheMarket()
        {
            float price = 1.4f;
            for (int i = 0; i < 50; i++)
                price = CompetitionMath.NextPrice(price, 1.4f, CompetitorMove.CutPrices);
            Assert.GreaterOrEqual(price, 1.4f * 0.9f - 0.01f);

            for (int i = 0; i < 50; i++)
                price = CompetitionMath.NextPrice(price, 1.4f, CompetitorMove.RaisePrices);
            Assert.LessOrEqual(price, 1.4f * 1.15f + 0.01f);
        }

        [Test]
        public void Buyout_NeedsLevelAndMoney()
        {
            Assert.IsFalse(CompetitionMath.CanBuyOut(CompetitionMath.BuyoutLevel - 1, 1e6f));
            Assert.IsFalse(CompetitionMath.CanBuyOut(CompetitionMath.BuyoutLevel, CompetitionMath.BuyoutPrice - 1f));
            Assert.IsTrue(CompetitionMath.CanBuyOut(CompetitionMath.BuyoutLevel, CompetitionMath.BuyoutPrice));
        }

        [Test]
        public void FinanceSave_RoundTrips()
        {
            var finance = new Finance
            {
                Loan = LoanKind.Large,
                LoanBalance = 12345f,
                WeeklyPayment = 2950f,
                Insured = true,
                WeekRevenue = 800f,
                DaysInDebt = 2
            };

            var json = JsonUtility.ToJson(FinanceSaveData.From(finance, Difficulty.Survival));
            var data = JsonUtility.FromJson<FinanceSaveData>(json);
            var restored = data.ToFinance();

            Assert.AreEqual(Difficulty.Survival, data.Difficulty);
            Assert.AreEqual(LoanKind.Large, restored.Loan);
            Assert.AreEqual(12345f, restored.LoanBalance);
            Assert.IsTrue(restored.Insured);
            Assert.AreEqual(2, restored.DaysInDebt);
        }

        [Test]
        public void CompetitorSave_RoundTrips()
        {
            var rival = new Competitor { Active = true, OpensOnDay = 3, Petrol92 = 1.3f, Petrol95 = 1.5f, Diesel = 1.35f, Reputation = 0.7f, Promo = CompetitorPromo.Advertising, PromoDaysLeft = 1 };
            var json = JsonUtility.ToJson(CompetitorSaveData.From(rival));
            var restored = new Competitor();
            JsonUtility.FromJson<CompetitorSaveData>(json).ApplyTo(ref restored);

            Assert.IsTrue(restored.Active);
            Assert.AreEqual(1.5f, restored.Price(FuelType.Petrol95));
            Assert.AreEqual(CompetitorPromo.Advertising, restored.Promo);
        }

        [Test]
        public void OldSave_StartsWithoutLoan_OnNormalDifficulty()
        {
            var data = JsonUtility.FromJson<SaveData>("{\"version\":12,\"day\":5}");
            Assert.IsTrue(data.IsSupported);
            // JsonUtility may create an empty object instead of null; both mean "no finance saved".
            var finance = data.finance != null ? data.finance.ToFinance() : default;
            Assert.AreEqual(LoanKind.None, finance.Loan);
            Assert.IsFalse(finance.Bankrupt);
            Assert.AreEqual(Difficulty.Normal, data.finance != null ? data.finance.Difficulty : Difficulty.Normal);
        }

        [Test]
        public void EveryDifficultyAndPromo_IsLocalized()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"difficulty.{difficulty}"), difficulty.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"difficulty.{difficulty}.text"), difficulty.ToString());
            }

            foreach (var promo in new[] { CompetitorPromo.Discount, CompetitorPromo.Advertising })
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"msg.competitorPromo.{promo}"), promo.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"panel.rival.promo.{promo}"), promo.ToString());
            }
        }
    }
}
