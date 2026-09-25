using System;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GasStation.Mono.Office
{
    /// <summary>Laptop apps for running the business: mail and letters, staff, suppliers, bank, competitor.</summary>
    public partial class LaptopController
    {
        private void BuildMail(VisualElement content)
        {
            BuildLetters(content);

            content.Add(Label(Loc.T("laptop.mail.offers"), "laptop-heading"));
            if (HudModel.Offers.Count == 0)
                content.Add(Label(Loc.T("laptop.mail.noOffers"), "laptop-muted"));

            int level = HudModel.Level.Level;
            bool full = HudModel.Contracts.Count >= ContractMath.MaxActive;
            foreach (var offer in HudModel.Offers)
            {
                var info = ContractMath.Get(offer.Type);
                var card = Card(GameTexts.ContractName(offer.Type));
                card.Add(Label(Loc.T($"contract.{offer.Type}.desc"), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.terms", ContractMath.VehiclesPerDay(offer.Type), GameTexts.FuelName(info.Fuel),
                    offer.Price, MarketPrice(info.Fuel), offer.Days), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.stakes", info.Penalty, info.Bonus, offer.ExpiresIn), "laptop-muted"));

                var actions = Actions(card);
                int id = offer.Id;
                var accept = Action(actions, Loc.T("laptop.contract.accept"), () => StationCommands.AcceptContract(id));
                accept.SetEnabled(!full && level >= info.RequiredLevel);
                Action(actions, Loc.T("laptop.contract.decline"), () => StationCommands.DeclineContract(id));
                if (level < info.RequiredLevel)
                    card.Add(Label(Loc.F("msg.contractNeedsLevel", info.RequiredLevel), "laptop-muted"));
                else if (full)
                    card.Add(Label(Loc.F("msg.contractsFull", ContractMath.MaxActive), "laptop-muted"));
            }

            content.Add(Label(Loc.F("laptop.mail.active", HudModel.Contracts.Count, ContractMath.MaxActive), "laptop-heading"));
            if (HudModel.Contracts.Count == 0)
                content.Add(Label(Loc.T("laptop.mail.noActive"), "laptop-muted"));

            foreach (var contract in HudModel.Contracts)
            {
                var info = ContractMath.Get(contract.Type);
                var card = Card(GameTexts.ContractName(contract.Type));
                card.Add(Label(Loc.F("laptop.contract.progress", contract.DaysLeft, contract.Served, contract.Missed,
                    ContractMath.StrikesToCancel, contract.Price), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.schedule", ScheduleText(contract.Type), GameTexts.FuelName(info.Fuel)), "laptop-muted"));

                var actions = Actions(card);
                int id = contract.Id;
                bool armed = _cancelArmedId == id && Time.unscaledTime - _cancelArmedAt < ConfirmTime * 3f;
                Action(actions, armed ? Loc.T("laptop.contract.confirmCancel") : Loc.F("laptop.contract.cancel", ContractMath.CancelPenalty(contract.Type)), () =>
                {
                    if (armed)
                    {
                        StationCommands.CancelContract(id);
                        _cancelArmedId = -1;
                    }
                    else
                    {
                        _cancelArmedId = id;
                        _cancelArmedAt = Time.unscaledTime;
                    }
                });
            }
        }

        /// <summary>"Inheritance": the story letters, the debt and PetroMax's offer.</summary>
        private void BuildLetters(VisualElement content)
        {
            var campaign = HudModel.Campaign;
            if (HudModel.Mode != GameMode.Campaign)
                return;

            content.Add(Label(Loc.T("laptop.letters"), "laptop-heading"));
            if (!campaign.Active)
            {
                var ending = Card(Loc.T($"letter.end.{campaign.Outcome}"));
                ending.Add(Label(Loc.T($"letter.end.{campaign.Outcome}.text"), "laptop-line"));
                return;
            }

            var goal = CampaignMath.Goal(campaign.Chapter);
            var letter = Card(Loc.T($"letter.{campaign.Chapter}.title"));
            letter.Add(Label(Loc.T($"letter.{campaign.Chapter}.text"), "laptop-line"));
            letter.Add(Label(Loc.F("laptop.letters.goal", goal.Deadline, goal.Repaid, campaign.Repaid, goal.Level), "laptop-line"));
            letter.Add(Label(Loc.F("laptop.letters.debt", CampaignMath.Remaining(campaign.Repaid)), "laptop-muted"));

            var actions = Actions(letter);
            foreach (float amount in new[] { 1000f, 5000f })
            {
                float pay = amount;
                var button = Action(actions, Loc.F("laptop.letters.pay", amount), () => StationCommands.RepayUncleDebt(pay));
                button.SetEnabled(HudModel.Economy.Money >= amount);
            }

            float all = CampaignMath.Payment(float.MaxValue, campaign.Repaid, HudModel.Economy.Money);
            var payAll = Action(actions, Loc.F("laptop.letters.payAll", all), () => StationCommands.RepayUncleDebt(all));
            payAll.SetEnabled(all > 0f);

            if (campaign.Chapter < 2 || campaign.OfferAnswered)
                return;

            var offer = Card(Loc.T("letter.offer.title"));
            offer.Add(Label(Loc.F("letter.offer.text", CampaignMath.BuyoutOffer), "laptop-line"));
            var answer = Actions(offer);
            Action(answer, Loc.T("letter.offer.decline"), () => StationCommands.AnswerBuyoutOffer(false));
            Action(answer, Loc.F("letter.offer.accept", CampaignMath.BuyoutOffer), () => StationCommands.AnswerBuyoutOffer(true));
        }

        private static string ScheduleText(ContractType type)
        {
            var info = ContractMath.Get(type);
            if (info.EveryHours <= 0)
                return Loc.F("laptop.contract.daily", info.FirstHour, info.VehiclesPerSlot);
            return Loc.F("laptop.contract.every", info.EveryHours, info.FirstHour, info.LastHour, info.VehiclesPerSlot);
        }

        private void BuildStaff(VisualElement content)
        {
            content.Add(Label(Loc.F("laptop.staff.heading", HudModel.Workers.Count, StaffMath.MaxStaff), "laptop-heading"));
            if (HudModel.Workers.Count == 0)
                content.Add(Label(Loc.T("panel.staff.none"), "laptop-muted"));

            foreach (var worker in HudModel.Workers)
            {
                var card = Card(Loc.F("laptop.staff.title", GameTexts.StaffName(worker.NameIndex), GameTexts.RoleName(worker.Role)));
                card.Add(Label(Loc.F("laptop.staff.line", worker.Skill, worker.Wage, worker.DaysWorked, Loc.T($"shift.{worker.Shift}"),
                    worker.Energy * 100f, GameTexts.MoodText(worker.Mood)), "laptop-line"));
                if (worker.Trait != StaffTrait.None)
                    card.Add(Label($"{GameTexts.TraitName(worker.Trait)}: {GameTexts.TraitDescription(worker.Trait)}", "laptop-muted"));

                var actions = Actions(card);
                int id = worker.Id;
                var praise = Action(actions, Loc.T("laptop.staff.praise"), () => StationCommands.PraiseWorker(id));
                praise.SetEnabled(worker.PraisedDay != HudModel.Day);
                var train = Action(actions, Loc.F("laptop.staff.train", StaffMath.TrainingCost(worker.Training)), () => StationCommands.TrainWorker(id));
                train.SetEnabled(StaffMath.CanTrain(worker) && HudModel.Economy.Money >= StaffMath.TrainingCost(worker.Training));
                Action(actions, Loc.F("laptop.staff.raise", StaffMath.Raise(worker.Wage)), () => StationCommands.RaiseWage(id));
                Action(actions, Loc.T("laptop.staff.shift"), () => StationCommands.ToggleShift(id));
                bool armed = _fireArmedId == id;
                Action(actions, armed ? Loc.T("laptop.staff.confirmFire") : Loc.T("laptop.staff.fire"), () =>
                {
                    if (armed)
                    {
                        StationCommands.FireWorker(id);
                        _fireArmedId = -1;
                    }
                    else
                    {
                        _fireArmedId = id;
                    }
                });
            }

            content.Add(Label(Loc.T("panel.staff.candidates"), "laptop-heading"));
            bool full = HudModel.Workers.Count >= StaffMath.MaxStaff;
            for (int i = 0; i < HudModel.Candidates.Count; i++)
            {
                var candidate = HudModel.Candidates[i];
                var card = Card(Loc.F("laptop.staff.title", GameTexts.StaffName(candidate.NameIndex), GameTexts.RoleName(candidate.Role)));
                card.Add(Label(Loc.F("laptop.staff.candidate", candidate.Skill, candidate.Wage,
                    GameTexts.ReferenceText(StaffMath.ReferenceGrade(candidate.Honesty))), "laptop-line"));
                card.Add(Label(GameTexts.RoleDuty(candidate.Role), "laptop-muted"));
                if (candidate.Trait != StaffTrait.None)
                    card.Add(Label($"{GameTexts.TraitName(candidate.Trait)}: {GameTexts.TraitDescription(candidate.Trait)}", "laptop-muted"));

                int index = i;
                float fee = StaffMath.HiringFee(candidate.Wage);
                var hire = Action(Actions(card), Loc.F("laptop.staff.hire", fee), () => StationCommands.HireCandidate(index));
                hire.SetEnabled(!full && HudModel.Economy.Money >= fee);
            }
        }

        private void BuildSuppliers(VisualElement content)
        {
            content.Add(Label(Loc.T("laptop.suppliers.heading"), "laptop-heading"));
            if (!HudModel.HasShop)
            {
                content.Add(Label(Loc.T("msg.noShop"), "laptop-muted"));
                return;
            }

            var supplierCard = Card(Loc.F("laptop.suppliers.current", Loc.T($"supplier.{HudModel.Supplier}")));
            foreach (var supplier in new[] { SupplierKind.Cheap, SupplierKind.Reliable })
                supplierCard.Add(Label($"{Loc.T($"supplier.{supplier}")}: {Loc.T($"supplier.{supplier}.desc")}", "laptop-line"));
            var choose = Actions(supplierCard);
            foreach (var supplier in new[] { SupplierKind.Cheap, SupplierKind.Reliable })
            {
                var kind = supplier;
                var button = Action(choose, Loc.T($"supplier.{supplier}"), () => StationCommands.SetSupplier(kind));
                button.SetEnabled(HudModel.Supplier != supplier);
            }

            if (HudModel.HasDiner)
            {
                int level = HudModel.Upgrades.Diner;
                var diner = Card(Loc.T("laptop.diner.title"));
                diner.Add(Label(Loc.F("laptop.diner.line", HudModel.DinerCounter[0].Ready,
                    DinerMath.OnMenu(DinerDish.Burger, level) ? HudModel.DinerCounter[1].Ready.ToString() : "—",
                    HudModel.Diner.Ingredients, DinerMath.IngredientCapacity(level)), "laptop-line"));
                diner.Add(Label(Loc.T("laptop.diner.hint"), "laptop-muted"));
                Action(Actions(diner), Loc.F("laptop.diner.order", DinerMath.IngredientOrder, DinerMath.IngredientOrder * DinerMath.IngredientPrice),
                    StationCommands.OrderIngredients);
            }

            float priceFactor = ShopMath.SupplierPriceFactor(HudModel.Supplier);
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                var type = (ProductType)i;
                var shelf = HudModel.Products[i];
                var card = Card(GameTexts.ProductName(type));
                card.Add(Label(Loc.F("laptop.suppliers.stock", shelf.Stock, shelf.Capacity, shelf.SellPrice,
                    shelf.BuyPrice * priceFactor, HudModel.PendingProducts[i]), "laptop-line"));
                float life = ShopMath.ShelfLife(type);
                if (life > 0f)
                    card.Add(Label(Loc.F(ShopMath.Expired(type, shelf.Age) && shelf.Stock > 0 ? "laptop.suppliers.expired" : "laptop.suppliers.age",
                        shelf.Age, life), "laptop-muted"));

                var actions = Actions(card);
                var product = type;
                Action(actions, Loc.F("laptop.suppliers.order", ShopMath.OrderSize, ShopMath.OrderSize * shelf.BuyPrice * priceFactor),
                    () => StationCommands.OrderProducts(product, ShopMath.OrderSize));
                Action(actions, Loc.T(shelf.Promo ? "laptop.suppliers.promoOff" : "laptop.suppliers.promoOn"),
                    () => StationCommands.TogglePromo(product));
            }
        }

        private void BuildBank(VisualElement content)
        {
            var finance = HudModel.Finance;
            content.Add(Label(Loc.F("laptop.bank.heading", Loc.T($"difficulty.{HudModel.Difficulty}")), "laptop-heading"));

            var loanCard = Card(Loc.T("laptop.bank.loan"));
            if (finance.Loan == LoanKind.None)
            {
                foreach (var kind in new[] { LoanKind.Small, LoanKind.Large })
                {
                    loanCard.Add(Label(Loc.F("laptop.bank.offer", FinanceMath.LoanAmount(kind), FinanceMath.LoanInterest(kind) * 100f,
                        FinanceMath.LoanWeeks(kind), FinanceMath.LoanWeeklyPayment(kind)), "laptop-line"));
                }

                var actions = Actions(loanCard);
                Action(actions, Loc.F("laptop.bank.take", FinanceMath.LoanAmount(LoanKind.Small)), () => StationCommands.TakeLoan(LoanKind.Small));
                Action(actions, Loc.F("laptop.bank.take", FinanceMath.LoanAmount(LoanKind.Large)), () => StationCommands.TakeLoan(LoanKind.Large));
            }
            else
            {
                loanCard.Add(Label(Loc.F("panel.bank.loan", finance.LoanBalance, finance.WeeklyPayment), "laptop-line"));
                float early = FinanceMath.EarlyRepayment(finance.LoanBalance, finance.Loan);
                var repay = Action(Actions(loanCard), Loc.F("laptop.bank.repay", early), StationCommands.RepayLoan);
                repay.SetEnabled(HudModel.Economy.Money >= early);
            }

            var insurance = Card(Loc.T("laptop.bank.insurance"));
            float premium = FinanceMath.InsurancePremium * FinanceMath.BillMultiplier(HudModel.Difficulty);
            insurance.Add(Label(Loc.F(finance.Insured ? "laptop.bank.insured" : "laptop.bank.notInsured", premium,
                FinanceMath.InsuranceCoverage * 100f), "laptop-line"));
            Action(Actions(insurance), Loc.T(finance.Insured ? "laptop.bank.insuranceOff" : "laptop.bank.insuranceOn"), StationCommands.ToggleInsurance);

            var bills = Card(Loc.T("laptop.bank.bills"));
            bills.Add(Label(Loc.F("panel.bank.utilities", HudModel.LastUtilities), "laptop-line"));
            int daysToTax = FinanceMath.DaysPerWeek - (HudModel.Day - 1) % FinanceMath.DaysPerWeek;
            bills.Add(Label(Loc.F("panel.bank.tax", FinanceMath.TaxRate * 100f, finance.WeekRevenue,
                FinanceMath.WeeklyTax(finance.WeekRevenue), daysToTax), "laptop-line"));
            if (finance.DaysInDebt > 0)
            {
                int limit = FinanceMath.BankruptcyDays(HudModel.Difficulty);
                bills.Add(Label(limit > 0 ? Loc.F("panel.bank.debt", finance.DaysInDebt, limit) : Loc.F("panel.bank.debtRelaxed", finance.DaysInDebt),
                    "laptop-line"));
            }
        }

        private void BuildCompetitor(VisualElement content)
        {
            var rival = HudModel.Competitor;
            content.Add(Label(Loc.T("laptop.rival.heading"), "laptop-heading"));
            if (rival.BoughtOut)
            {
                content.Add(Label(Loc.F("panel.rival.boughtOut", StationProfile.DisplayName), "laptop-line"));
                return;
            }

            if (!rival.Active)
            {
                content.Add(Label(Loc.F("panel.rival.notOpen", rival.OpensOnDay), "laptop-line"));
                return;
            }

            var prices = Card(Loc.T("laptop.rival.prices"));
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = (FuelType)i;
                prices.Add(Label(Loc.F("laptop.rival.priceLine", GameTexts.FuelName(fuel), HudModel.Fuel[i].SellPrice, rival.Price(fuel),
                    HudModel.Fuel[i].MarketPrice), "laptop-line"));
            }

            var market = Card(Loc.T("laptop.rival.market"));
            market.Add(Label(Loc.F("panel.rival.share", rival.OurShare * 100f), "laptop-line"));
            market.Add(Label(Loc.F("panel.rival.reputation", HudModel.Economy.Reputation * 100f, rival.Reputation * 100f), "laptop-line"));
            if (rival.Promo != CompetitorPromo.None)
                market.Add(Label(Loc.F($"panel.rival.promo.{rival.Promo}", rival.PromoDaysLeft), "laptop-line"));

            var buyout = Card(Loc.T("laptop.rival.buyout"));
            bool canBuy = CompetitionMath.CanBuyOut(HudModel.Level.Level, HudModel.Economy.Money);
            buyout.Add(Label(HudModel.Level.Level >= CompetitionMath.BuyoutLevel
                ? Loc.F("laptop.rival.buyoutText", CompetitionMath.BuyoutPrice)
                : Loc.F("panel.rival.buyoutLocked", CompetitionMath.BuyoutPrice, CompetitionMath.BuyoutLevel), "laptop-line"));
            var button = Action(Actions(buyout), _buyoutArmed ? Loc.T("laptop.rival.confirm") : Loc.F("laptop.rival.buy", CompetitionMath.BuyoutPrice), () =>
            {
                if (_buyoutArmed)
                {
                    StationCommands.BuyOutCompetitor();
                    _buyoutArmed = false;
                }
                else
                {
                    _buyoutArmed = true;
                }
            });
            button.SetEnabled(canBuy);
        }

        private static float MarketPrice(FuelType fuel) => HudModel.Fuel[(int)fuel].MarketPrice;
    }
}
