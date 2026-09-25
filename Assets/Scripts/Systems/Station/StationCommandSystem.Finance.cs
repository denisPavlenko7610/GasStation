using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Money commands: loans, insurance, the competitor buyout and the campaign debt.</summary>
    public partial class StationCommandSystem
    {
        /// <summary>Rules of the new game: difficulty and mode; the sandbox starts rich, the campaign starts chapter one.</summary>
        private void StartMode(Entity station, Difficulty difficulty, GameMode mode)
        {
            if (mode == GameMode.Sandbox)
                difficulty = Difficulty.Relaxed;
            if (SystemAPI.HasComponent<StationRules>(station))
                SystemAPI.SetComponent(station, new StationRules { Difficulty = difficulty, Mode = mode });

            if (mode == GameMode.Sandbox)
                SystemAPI.GetComponentRW<Economy>(station).ValueRW.Money = CampaignMath.SandboxMoney;

            if (SystemAPI.HasComponent<Campaign>(station))
                SystemAPI.SetComponent(station, new Campaign { Active = mode == GameMode.Campaign, Chapter = 1 });
        }

        private void TakeLoan(Entity station, LoanKind kind)
        {
            if (!SystemAPI.HasComponent<Finance>(station) || kind == LoanKind.None)
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            if (finance.ValueRO.Loan != LoanKind.None)
            {
                HudModel.Notify(Loc.T("msg.loanAlready"));
                return;
            }

            float amount = FinanceMath.LoanAmount(kind);
            finance.ValueRW.Loan = kind;
            finance.ValueRW.LoanBalance = FinanceMath.LoanTotal(kind);
            finance.ValueRW.WeeklyPayment = FinanceMath.LoanWeeklyPayment(kind);

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            economy.ValueRW.Money += amount;

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.LoanTaken, default, amount);
            HudModel.Notify(Loc.F("msg.loanTaken", amount, FinanceMath.LoanWeeklyPayment(kind), FinanceMath.LoanWeeks(kind)));
        }

        private void RepayLoan(Entity station)
        {
            if (!SystemAPI.HasComponent<Finance>(station))
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            if (finance.ValueRO.Loan == LoanKind.None)
            {
                HudModel.Notify(Loc.T("msg.noLoan"));
                return;
            }

            float cost = FinanceMath.EarlyRepayment(finance.ValueRO.LoanBalance, finance.ValueRO.Loan);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            finance.ValueRW.Loan = LoanKind.None;
            finance.ValueRW.LoanBalance = 0f;
            finance.ValueRW.WeeklyPayment = 0f;

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.LoanRepaid, default, cost);
            HudModel.Notify(Loc.F("msg.loanRepaid", cost));
        }

        private void ToggleInsurance(Entity station)
        {
            if (!SystemAPI.HasComponent<Finance>(station))
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            finance.ValueRW.Insured = !finance.ValueRO.Insured;
            HudModel.Notify(finance.ValueRO.Insured
                ? Loc.F("msg.insuranceOn", FinanceMath.InsurancePremium)
                : Loc.T("msg.insuranceOff"));
        }

        private void BuyOutCompetitor(Entity station)
        {
            if (!SystemAPI.HasComponent<Competitor>(station))
                return;

            var rival = SystemAPI.GetComponentRW<Competitor>(station);
            if (!rival.ValueRO.Active || rival.ValueRO.BoughtOut)
                return;

            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (stationLevel < CompetitionMath.BuyoutLevel)
            {
                HudModel.Notify(Loc.F("msg.buyoutNeedsLevel", CompetitionMath.BuyoutLevel));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < CompetitionMath.BuyoutPrice)
            {
                HudModel.Notify(Loc.F("msg.noMoney", CompetitionMath.BuyoutPrice));
                return;
            }

            economy.ValueRW.Money -= CompetitionMath.BuyoutPrice;
            economy.ValueRW.DayExpenses += CompetitionMath.BuyoutPrice;
            rival.ValueRW.BoughtOut = true;
            rival.ValueRW.Promo = CompetitorPromo.None;
            rival.ValueRW.OurShare = 1f;

            // Regulars who left for PetroMax have nowhere else to go now.
            if (SystemAPI.HasBuffer<RegularState>(station))
            {
                var regulars = SystemAPI.GetBuffer<RegularState>(station);
                for (int i = 0; i < regulars.Length; i++)
                {
                    var regular = regulars[i];
                    if (!regular.Lost)
                        continue;
                    regular.Lost = false;
                    regular.Upsets = 0;
                    regular.Loyalty = VisitorMath.StartLoyalty;
                    regulars[i] = regular;
                }
            }

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.CompetitorBoughtOut, default, CompetitionMath.BuyoutPrice);
        }

        private void RepayUncleDebt(Entity station, float wanted)
        {
            if (!SystemAPI.HasComponent<Campaign>(station))
                return;

            var campaign = SystemAPI.GetComponent<Campaign>(station);
            if (!campaign.Active)
                return;

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float amount = CampaignMath.Payment(wanted, campaign.Repaid, economy.ValueRO.Money);
            if (amount <= 0f)
            {
                HudModel.Notify(Loc.F("msg.noMoney", wanted));
                return;
            }

            economy.ValueRW.Money -= amount;
            economy.ValueRW.DayExpenses += amount;
            campaign.Repaid += amount;
            SystemAPI.SetComponent(station, campaign);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.CampaignPayment, default, amount);
            HudModel.Notify(Loc.F("msg.debtPaid", amount, CampaignMath.Remaining(campaign.Repaid)));
        }

        /// <summary>PetroMax wants the land. Selling ends the story; declining keeps it going.</summary>
        private void AnswerBuyoutOffer(Entity station, bool accept)
        {
            if (!SystemAPI.HasComponent<Campaign>(station))
                return;

            var campaign = SystemAPI.GetComponent<Campaign>(station);
            if (!campaign.Active || campaign.Chapter < 2 || campaign.OfferAnswered)
                return;

            campaign.OfferAnswered = true;
            if (accept)
            {
                SystemAPI.GetComponentRW<Economy>(station).ValueRW.Money += CampaignMath.BuyoutOffer;
                campaign.Active = false;
                campaign.Outcome = CampaignOutcome.Sold;
            }

            SystemAPI.SetComponent(station, campaign);
            HudModel.Notify(Loc.T(accept ? "msg.soldLand" : "msg.declinedOffer"));
        }
    }
}
