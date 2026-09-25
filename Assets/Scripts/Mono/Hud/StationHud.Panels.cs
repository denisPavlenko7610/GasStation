using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Mono.Hud;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>Side panels opened with hotkeys: upgrades, shop, paint, staff, achievements, finances, bank, competitor, regulars, build mode.</summary>
    public partial class StationHud
    {
        private string BuildUpgrades()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.upgrades", _upgradePage + 1, UpgradePages()));
            for (int slot = 0; slot < UpgradesPerPage; slot++)
            {
                int i = _upgradePage * UpgradesPerPage + slot;
                if (i >= UpgradeMath.Purchasable.Length)
                    break;

                var type = UpgradeMath.Purchasable[i];
                int level = HudModel.Upgrades.Get(type);
                int requiredLevel = ProgressMath.RequiredLevel(type, level);
                string price = !UpgradeMath.CanUpgrade(type, level) ? Loc.T("panel.max")
                    : HudModel.Level.Level < requiredLevel ? Loc.F("panel.needLevel", requiredLevel)
                    : Loc.F("panel.price", UpgradeMath.Cost(type, level));
                _builder.AppendLine(Loc.F("panel.upgrades.line", slot + 1, GameTexts.UpgradeName(type), level,
                    UpgradeMath.MaxLevel(type), price, GameTexts.UpgradeDescription(type)));
            }

            return _builder.ToString();
        }

        private string BuildStaff()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.staff", HudModel.Workers.Count, StaffMath.MaxStaff));
            _builder.AppendLine(Loc.T("panel.staff.candidates"));
            for (int i = 0; i < HudModel.Candidates.Count; i++)
            {
                var candidate = HudModel.Candidates[i];
                _builder.AppendLine(Loc.F("panel.staff.candidate", i + 1, GameTexts.StaffName(candidate.NameIndex),
                    GameTexts.RoleName(candidate.Role), GameTexts.RoleDuty(candidate.Role), candidate.Skill, candidate.Wage,
                    GameTexts.ReferenceText(StaffMath.ReferenceGrade(candidate.Honesty)), StaffMath.HiringFee(candidate.Wage)));
                if (candidate.Trait != StaffTrait.None)
                    _builder.AppendLine("    " + GameTexts.TraitName(candidate.Trait));
            }

            if (HudModel.Workers.Count == 0)
            {
                _builder.Append(Loc.T("panel.staff.none"));
                return _builder.ToString();
            }

            _builder.AppendLine(Loc.T("panel.staff.working"));
            for (int slot = 0; slot < HudModel.Workers.Count && slot < StaffMath.MaxStaff; slot++)
            {
                var worker = HudModel.Workers[slot];
                _builder.AppendLine(Loc.F("panel.staff.worker", slot + 4, GameTexts.StaffName(worker.NameIndex),
                    GameTexts.RoleName(worker.Role), worker.Skill, worker.Wage, worker.DaysWorked));
                _builder.AppendLine(Loc.F("panel.staff.state", Loc.T($"shift.{worker.Shift}"),
                    StaffMath.OnShift(worker.Shift, HudModel.Hour) ? Loc.T("panel.staff.onShift") : Loc.T("panel.staff.offShift"),
                    worker.Energy * 100f, GameTexts.MoodText(worker.Mood), GameTexts.TraitName(worker.Trait)));
            }

            _builder.Append(Loc.T("panel.staff.laptopHint"));

            return _builder.ToString();
        }

        private string BuildPaint()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.T("panel.paint"));
            _builder.AppendLine(Loc.F("panel.paint.current", GameTexts.SchemeName(HudModel.PaintScheme),
                TrafficPercent(HudModel.PaintScheme)));
            for (int scheme = 1; scheme < StyleMath.SchemeCount; scheme++)
            {
                int required = StyleMath.RequiredLevel(scheme);
                string price = scheme == HudModel.PaintScheme ? Loc.T("panel.paint.current.short")
                    : HudModel.Level.Level < required ? Loc.F("panel.needLevel", required)
                    : Loc.F("panel.price", StyleMath.Cost(scheme));
                _builder.AppendLine(Loc.F("panel.paint.line", scheme, GameTexts.SchemeName(scheme), price, TrafficPercent(scheme)));
            }

            return _builder.ToString();
        }

        private static float TrafficPercent(int scheme) => StyleMath.TrafficFactor(scheme) * 100f - 100f;

        private string BuildStore()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.store", ShopMath.OrderSize));
            _builder.AppendLine(Loc.F("panel.store.inside", HudModel.PedestriansInShop));
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                var product = HudModel.Products[i];
                if ((int)_selectedProduct == i)
                    _builder.Append("▶ ");
                _builder.Append(Loc.F("panel.store.line", i + 1, GameTexts.ProductName((ProductType)i), product.Stock,
                    product.Capacity, product.SellPrice, product.BuyPrice, product.ReferencePrice));
                if (HudModel.PendingProducts[i] > 0)
                    _builder.Append(Loc.F("panel.store.pending", HudModel.PendingProducts[i]));
                float life = ShopMath.ShelfLife((ProductType)i);
                if (life > 0f && product.Stock > 0)
                    _builder.Append(Loc.F(ShopMath.Expired((ProductType)i, product.Age) ? "panel.store.expired" : "panel.store.age", product.Age, life));
                if (product.Promo)
                    _builder.Append(Loc.T("panel.store.promo"));
                _builder.AppendLine();
            }

            _builder.Append(Loc.F("panel.store.supplier", Loc.T($"supplier.{HudModel.Supplier}")));
            if (HudModel.HasDiner)
            {
                int level = HudModel.Upgrades.Diner;
                _builder.AppendLine();
                _builder.Append(Loc.F("panel.store.diner", HudModel.DinerCounter[0].Ready,
                    DinerMath.OnMenu(DinerDish.Burger, level) ? HudModel.DinerCounter[1].Ready.ToString() : "—",
                    HudModel.Diner.Ingredients, DinerMath.IngredientCapacity(level), DinerMath.IngredientOrder,
                    DinerMath.IngredientOrder * DinerMath.IngredientPrice));
            }

            return _builder.ToString();
        }

        private string BuildAchievements()
        {
            var context = new AchievementContext
            {
                Stats = HudModel.Stats,
                Level = HudModel.Level.Level,
                Money = HudModel.Economy.Money,
                Cleanliness = HudModel.Cleanliness.Value,
                Headcount = HudModel.Staff.Headcount,
                RenovationsTotal = HudModel.RenovationsTotal,
                RenovationsDone = CountBits(HudModel.RenovationsDone)
            };

            int unlocked = 0;
            for (int i = 0; i < AchievementCatalog.Count; i++)
            {
                if (HudModel.Achievements.Has(i))
                    unlocked++;
            }

            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.achievements", unlocked, AchievementCatalog.Count));
            for (int i = 0; i < AchievementCatalog.Count; i++)
            {
                var id = (AchievementId)i;
                bool done = HudModel.Achievements.Has(i);
                var progress = AchievementCatalog.Progress(id, context);
                string state = done ? "✔" : $"{Mathf.Min(progress.x, progress.y):0}/{progress.y:0}";
                _builder.AppendLine(Loc.F("panel.achievements.line", state, Loc.T($"achievement.{id}.name"), Loc.T($"achievement.{id}.desc")));
            }

            _builder.Append(Loc.F("panel.achievements.stats", HudModel.Stats.DaysPlayed, HudModel.Stats.Served, HudModel.Stats.Income));
            return _builder.ToString();
        }

        private static int CountBits(ulong mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        private string BuildFinance()
        {
            var economy = HudModel.Economy;
            _builder.Clear();
            _builder.AppendLine(Loc.T("panel.finance"));
            _builder.AppendLine(Loc.F("panel.finance.today", economy.DayIncome, economy.DayExpenses, economy.DayIncome - economy.DayExpenses));

            float weekIncome = 0f, weekExpenses = 0f;
            int from = System.Math.Max(0, HudModel.History.Count - 7);
            for (int i = from; i < HudModel.History.Count; i++)
            {
                weekIncome += HudModel.History[i].Income;
                weekExpenses += HudModel.History[i].Expenses;
            }

            if (HudModel.History.Count > 0)
            {
                var yesterday = HudModel.History[HudModel.History.Count - 1];
                _builder.AppendLine(Loc.F("panel.finance.yesterday", yesterday.Income, yesterday.Expenses, yesterday.Income - yesterday.Expenses));
                _builder.AppendLine(Loc.F("panel.finance.week", HudModel.History.Count - from, weekIncome, weekExpenses, weekIncome - weekExpenses));
            }

            var stats = HudModel.Stats;
            float rating = stats.RatingCount > 0 ? stats.RatingSum / stats.RatingCount : 0f;
            _builder.AppendLine(stats.RatingCount > 0
                ? Loc.F("panel.finance.rating", GameTexts.Stars(Mathf.RoundToInt(rating)), rating, stats.RatingCount)
                : Loc.T("panel.finance.noRating"));

            if (HudModel.Reviews.Count > 0)
            {
                _builder.AppendLine(Loc.T("panel.finance.reviews"));
                for (int i = 0; i < HudModel.Reviews.Count && i < 4; i++)
                {
                    var review = HudModel.Reviews[i];
                    string author = review.RegularId > 0 ? GameTexts.RegularName(review.RegularId) + ": " : string.Empty;
                    _builder.AppendLine($"{GameTexts.Stars(review.Stars)}  {author}{Loc.T($"review.{review.Stars}.{review.Variant}")}");
                }
            }

            _builder.Append(HudModel.History.Count > 0 ? Loc.T("panel.finance.chart") : Loc.T("panel.finance.noHistory"));
            return _builder.ToString();
        }

        private string BuildBank()
        {
            var finance = HudModel.Finance;
            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.bank", Loc.T($"difficulty.{HudModel.Difficulty}")));

            if (finance.Loan == LoanKind.None)
            {
                _builder.AppendLine(Loc.T("panel.bank.noLoan"));
                AppendLoanOffer(1, LoanKind.Small);
                AppendLoanOffer(2, LoanKind.Large);
            }
            else
            {
                _builder.AppendLine(Loc.F("panel.bank.loan", finance.LoanBalance, finance.WeeklyPayment));
                _builder.AppendLine(Loc.F("panel.bank.repay",
                    FinanceMath.EarlyRepayment(finance.LoanBalance, finance.Loan)));
            }

            _builder.AppendLine(finance.Insured
                ? Loc.F("panel.bank.insured", FinanceMath.InsurancePremium * FinanceMath.BillMultiplier(HudModel.Difficulty), FinanceMath.InsuranceCoverage * 100f)
                : Loc.F("panel.bank.notInsured", FinanceMath.InsurancePremium * FinanceMath.BillMultiplier(HudModel.Difficulty), FinanceMath.InsuranceCoverage * 100f));

            _builder.AppendLine(Loc.F("panel.bank.utilities", HudModel.LastUtilities));
            int daysToTax = FinanceMath.DaysPerWeek - (HudModel.Day - 1) % FinanceMath.DaysPerWeek;
            _builder.AppendLine(Loc.F("panel.bank.tax", FinanceMath.TaxRate * 100f, finance.WeekRevenue,
                FinanceMath.WeeklyTax(finance.WeekRevenue), daysToTax));

            if (finance.DaysInDebt > 0)
            {
                int limit = FinanceMath.BankruptcyDays(HudModel.Difficulty);
                _builder.AppendLine(limit > 0
                    ? Loc.F("panel.bank.debt", finance.DaysInDebt, limit)
                    : Loc.F("panel.bank.debtRelaxed", finance.DaysInDebt));
            }

            _builder.Append(Loc.T("panel.bank.help"));
            return _builder.ToString();
        }

        private void AppendLoanOffer(int key, LoanKind kind) =>
            _builder.AppendLine(Loc.F("panel.bank.offer", key, FinanceMath.LoanAmount(kind), FinanceMath.LoanInterest(kind) * 100f,
                FinanceMath.LoanWeeks(kind), FinanceMath.LoanWeeklyPayment(kind)));

        private string BuildCompetitor()
        {
            var rival = HudModel.Competitor;
            _builder.Clear();
            _builder.AppendLine(Loc.T("panel.rival"));

            if (rival.BoughtOut)
            {
                _builder.Append(Loc.F("panel.rival.boughtOut", StationProfile.DisplayName));
                return _builder.ToString();
            }

            if (!rival.Active)
            {
                _builder.Append(Loc.F("panel.rival.notOpen", rival.OpensOnDay));
                return _builder.ToString();
            }

            _builder.AppendLine(Loc.T("panel.rival.header"));
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = (FuelType)i;
                float ours = HudModel.Fuel[i].SellPrice;
                float theirs = rival.Price(fuel);
                string mark = ours < theirs - 0.005f ? "▲" : ours > theirs + 0.005f ? "▼" : "=";
                _builder.AppendLine($"{GameTexts.FuelName(fuel),-10} ${ours:0.00}   ${theirs:0.00}  {mark}");
            }

            _builder.AppendLine(Loc.F("panel.rival.reputation", HudModel.Economy.Reputation * 100f, rival.Reputation * 100f));
            if (rival.Promo != CompetitorPromo.None)
                _builder.AppendLine(Loc.F($"panel.rival.promo.{rival.Promo}", rival.PromoDaysLeft));
            _builder.AppendLine(Loc.F("panel.rival.share", rival.OurShare * 100f));

            _builder.Append(HudModel.Level.Level >= CompetitionMath.BuyoutLevel
                ? Loc.F("panel.rival.buyout", CompetitionMath.BuyoutPrice)
                : Loc.F("panel.rival.buyoutLocked", CompetitionMath.BuyoutPrice, CompetitionMath.BuyoutLevel));
            return _builder.ToString();
        }

        private string BuildRegulars()
        {
            int met = 0;
            foreach (var regular in HudModel.Regulars)
            {
                if (regular.Visits > 0)
                    met++;
            }

            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.regulars", met, HudModel.Regulars.Count));
            if (HudModel.Buzz.DaysLeft > 0)
                _builder.AppendLine(Loc.F(HudModel.Buzz.Factor > 1f ? "panel.regulars.praise" : "panel.regulars.pan", HudModel.Buzz.DaysLeft));

            for (int i = 0; i < HudModel.Regulars.Count && i < RegularCatalog.Count; i++)
            {
                var regular = HudModel.Regulars[i];
                if (regular.Visits == 0)
                    continue;

                var info = RegularCatalog.Get(i);
                string status = regular.Lost ? Loc.T("panel.regulars.lost")
                    : regular.Upsets > 0 ? Loc.F("panel.regulars.upset", regular.Upsets, VisitorMath.UpsetsToLeave)
                    : GameTexts.Hearts(regular.Loyalty);
                _builder.AppendLine(Loc.F("panel.regulars.line", GameTexts.RegularName(i + 1), GameTexts.FuelName(info.Fuel),
                    DaysText(info.Days), info.Hour, regular.Visits, status));
            }

            if (met == 0)
                _builder.AppendLine(Loc.T("panel.regulars.none"));
            _builder.Append(Loc.T("panel.regulars.help"));
            return _builder.ToString();
        }

        /// <summary>When a regular comes: every day, weekdays, weekends or a list of days.</summary>
        private static string DaysText(byte days) => days switch
        {
            RegularCatalog.EveryDay => Loc.T("weekday.every"),
            RegularCatalog.Weekdays => Loc.T("weekday.weekdays"),
            RegularCatalog.Weekend => Loc.T("weekday.weekend"),
            _ => DayList(days)
        };

        private static string DayList(byte days)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < 7; i++)
            {
                if ((days & (1 << i)) == 0)
                    continue;
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(Loc.T($"weekday.{i}"));
            }

            return builder.ToString();
        }

        private string BuildBuildMode()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("panel.build", HudModel.Props.Count, PropMath.MaxProps));
            int level = HudModel.Level.Level;
            for (int i = 0; i < PropTypes.Count; i++)
            {
                var type = (PropType)i;
                var info = PropMath.Get(type);
                string marker = type == BuildMode.Selected ? "▶" : "  ";
                string line = level >= info.RequiredLevel
                    ? Loc.F("panel.build.item", i + 1, GameTexts.PropName(type), info.Cost, HudModel.PropEffects.Get(type))
                    : Loc.F("panel.build.locked", i + 1, GameTexts.PropName(type), info.RequiredLevel);
                _builder.Append(marker).AppendLine(line);
            }

            _builder.AppendLine(GameTexts.PropDescription(BuildMode.Selected));
            var error = Build.BuildModeController.CurrentError;
            if (error != PlacementError.None)
                _builder.AppendLine(error switch
                {
                    PlacementError.NeedsLevel => Loc.F("msg.propNeedsLevel", GameTexts.PropName(BuildMode.Selected),
                        PropMath.Get(BuildMode.Selected).RequiredLevel),
                    PlacementError.NoMoney => Loc.F("msg.noMoney", PropMath.Get(BuildMode.Selected).Cost),
                    _ => Loc.T($"build.error.{error}")
                });
            _builder.Append(Loc.T("panel.build.help"));
            return _builder.ToString();
        }
    }
}
