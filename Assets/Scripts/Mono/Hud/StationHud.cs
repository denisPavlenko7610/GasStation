using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Mono.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono
{
    /// <summary>
    /// Game HUD: handles keys, reads HudModel, sends StationCommands and builds localized texts (Loc).
    /// Drawing is done by an IHudView — UI Toolkit when its assets are in Resources/UI, uGUI otherwise.
    /// </summary>
    public class StationHud : MonoBehaviour
    {
        private const float PriceStep = 0.05f;
        private const float OrderLiters = 500f;
        private const float MessageDuration = 3f;
        private const float ReportDuration = 8f;
        private const float ConfirmTime = 2f;
        private const float ProductPriceStep = 0.25f;
        private const int UpgradesPerPage = 6;

        private readonly StringBuilder _builder = new();
        private IHudView _view;

        private bool _upgradesOpen;
        private int _upgradePage;
        private bool _storeOpen;
        private bool _paintOpen;
        private bool _staffOpen;
        private bool _achievementsOpen;
        private bool _financeOpen;
        /// <summary>0 = overview, 1 = bank, 2 = competitor.</summary>
        private int _financePage;
        private const int FinancePages = 3;
        private float _buyoutRequestedAt = float.NegativeInfinity;
        private readonly System.Collections.Generic.List<string> _cardTexts = new();
        private int _fireRequestedId = -1;
        private float _fireRequestedAt = float.NegativeInfinity;
        private float _newGameRequestedAt = float.NegativeInfinity;
        private ProductType _selectedProduct;
        private FuelType _selectedFuel;
        private int _shownReportDay;
        private float _reportShownAt = float.NegativeInfinity;

        private void Awake()
        {
            _view = ToolkitHudView.TryCreate(gameObject) ?? new UguiHudView(gameObject);
        }

        private void Update()
        {
            _view.SetVisible(HudModel.HasStation);
            if (!HudModel.HasStation)
                return;

            if (!GamePause.MenuOpen && !BuildMode.Active)
                HandleKeys();
            _view.SetText(HudBlock.Status, BuildStatus());
            _view.SetText(HudBlock.Fuel, BuildFuel());
            _view.SetText(HudBlock.Pumps, BuildPumps());
            _view.SetText(HudBlock.Center, BuildCenter());
            _view.SetText(HudBlock.Help, GameSettings.ShowControls ? Loc.T("hud.help") : string.Empty);
            _view.SetText(HudBlock.Panel, BuildMode.Active ? BuildBuildMode()
                : _upgradesOpen ? BuildUpgrades()
                : _storeOpen ? BuildStore()
                : _paintOpen ? BuildPaint()
                : _staffOpen ? BuildStaff()
                : _achievementsOpen ? BuildAchievements()
                : _financeOpen ? _financePage switch { 1 => BuildBank(), 2 => BuildCompetitor(), _ => BuildFinance() }
                : BuildQuest());
            _view.SetChart(_financeOpen && _financePage == 0 && !BuildMode.Active ? HudModel.History : null);
            UpdateCards();

            _view.SetMarker(HudModel.HasQuestTarget && !_upgradesOpen && !_storeOpen, HudModel.QuestTarget);

            var level = HudModel.Level;
            _view.SetMeters(new HudMeters
            {
                Reputation = HudModel.Economy.Reputation,
                Cleanliness = HudModel.Cleanliness.Value,
                Experience = level.Level >= ProgressMath.MaxLevel ? 1f : level.Experience / ProgressMath.ExperienceToNext(level.Level)
            });
        }

        // ---------------------------------------------------------------- input

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                // Tab: page 1 → page 2 → closed.
                int pages = UpgradePages();
                if (!_upgradesOpen)
                    OpenOnly(ref _upgradesOpen);
                else if (++_upgradePage >= pages)
                    _upgradesOpen = false;
            }

            if (keyboard.mKey.wasPressedThisFrame && HudModel.HasShop)
                Toggle(ref _storeOpen);
            if (keyboard.cKey.wasPressedThisFrame)
                Toggle(ref _paintOpen);
            if (keyboard.hKey.wasPressedThisFrame)
                Toggle(ref _staffOpen);
            if (keyboard.jKey.wasPressedThisFrame)
                Toggle(ref _achievementsOpen);
            if (keyboard.fKey.wasPressedThisFrame)
            {
                // F: overview → bank → competitor → closed.
                if (!_financeOpen)
                {
                    OpenOnly(ref _financeOpen);
                    _financePage = 0;
                }
                else if (++_financePage >= FinancePages)
                {
                    _financeOpen = false;
                }
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                GamePause.SetGameSpeed(GameSettings.GameSpeed % 3 + 1);
                GameSettings.Save();
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                GameSettings.ShowControls = !GameSettings.ShowControls;
                GameSettings.Save();
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                Loc.ToggleLanguage();
                HudModel.Notify(Loc.T("msg.language"));
            }

            if (keyboard.f5Key.wasPressedThisFrame)
                StationCommands.SaveGame();
            if (keyboard.f9Key.wasPressedThisFrame)
                StationCommands.LoadGame();
            if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (Time.unscaledTime - _newGameRequestedAt < ConfirmTime)
                {
                    StationCommands.NewGame(HudModel.Difficulty);
                    _newGameRequestedAt = float.NegativeInfinity;
                }
                else
                {
                    _newGameRequestedAt = Time.unscaledTime;
                    HudModel.Notify(Loc.T("msg.confirmNewGame"));
                }
            }

            if (_staffOpen)
                HandleStaffKeys(keyboard);
            else if (_paintOpen)
                HandlePaintKeys(keyboard);
            else if (_upgradesOpen)
                HandleUpgradeKeys(keyboard);
            else if (_storeOpen)
                HandleStoreKeys(keyboard);
            else if (_financeOpen && _financePage == 1)
                HandleBankKeys(keyboard);
            else if (_financeOpen && _financePage == 2)
                HandleCompetitorKeys(keyboard);
            else
                HandleFuelKeys(keyboard);
        }

        private void Toggle(ref bool panel)
        {
            bool open = !panel;
            CloseAll();
            panel = open;
        }

        private void OpenOnly(ref bool panel)
        {
            CloseAll();
            panel = true;
            _upgradePage = 0;
        }

        private void CloseAll()
        {
            _upgradesOpen = false;
            _storeOpen = false;
            _paintOpen = false;
            _staffOpen = false;
            _achievementsOpen = false;
            _financeOpen = false;
        }

        private void HandleFuelKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol92;
            if (keyboard.digit2Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol95;
            if (keyboard.digit3Key.wasPressedThisFrame) _selectedFuel = FuelType.Diesel;

            if (PlusPressed(keyboard))
                StationCommands.ChangePrice(_selectedFuel, PriceStep);
            if (MinusPressed(keyboard))
                StationCommands.ChangePrice(_selectedFuel, -PriceStep);
            if (keyboard.oKey.wasPressedThisFrame)
                StationCommands.OrderFuel(_selectedFuel, OrderLiters);
        }

        private void HandleBankKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) StationCommands.TakeLoan(LoanKind.Small);
            if (keyboard.digit2Key.wasPressedThisFrame) StationCommands.TakeLoan(LoanKind.Large);
            if (keyboard.digit3Key.wasPressedThisFrame) StationCommands.RepayLoan();
            if (keyboard.digit4Key.wasPressedThisFrame) StationCommands.ToggleInsurance();
        }

        private void HandleCompetitorKeys(Keyboard keyboard)
        {
            if (!keyboard.digit1Key.wasPressedThisFrame || !HudModel.Competitor.Active || HudModel.Competitor.BoughtOut)
                return;

            // Buying out costs a fortune, so it needs a second press within two seconds.
            if (Time.unscaledTime - _buyoutRequestedAt < ConfirmTime)
            {
                StationCommands.BuyOutCompetitor();
                _buyoutRequestedAt = float.NegativeInfinity;
            }
            else
            {
                _buyoutRequestedAt = Time.unscaledTime;
                HudModel.Notify(Loc.F("panel.rival.confirmBuyout", CompetitionMath.BuyoutPrice));
            }
        }

        private void HandleStoreKeys(Keyboard keyboard)
        {
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                if (DigitPressed(keyboard, i + 1))
                    _selectedProduct = (ProductType)i;
            }

            if (PlusPressed(keyboard))
                StationCommands.ChangeProductPrice(_selectedProduct, ProductPriceStep);
            if (MinusPressed(keyboard))
                StationCommands.ChangeProductPrice(_selectedProduct, -ProductPriceStep);
            if (keyboard.oKey.wasPressedThisFrame)
                StationCommands.OrderProducts(_selectedProduct, ShopMath.OrderSize);
        }

        private void HandleUpgradeKeys(Keyboard keyboard)
        {
            for (int slot = 0; slot < UpgradesPerPage; slot++)
            {
                int index = _upgradePage * UpgradesPerPage + slot;
                if (index < UpgradeMath.Purchasable.Length && DigitPressed(keyboard, slot + 1))
                    StationCommands.BuyUpgrade(UpgradeMath.Purchasable[index]);
            }
        }

        private void HandlePaintKeys(Keyboard keyboard)
        {
            for (int scheme = 1; scheme < StyleMath.SchemeCount; scheme++)
            {
                if (DigitPressed(keyboard, scheme))
                    StationCommands.PaintStation(scheme);
            }
        }

        private void HandleStaffKeys(Keyboard keyboard)
        {
            for (int i = 0; i < StaffMath.CandidatesPerDay; i++)
            {
                if (DigitPressed(keyboard, i + 1))
                    StationCommands.HireCandidate(i);
            }

            // Workers are listed as 4..9; firing needs a second press within two seconds.
            for (int slot = 0; slot < StaffMath.MaxStaff && slot < HudModel.Workers.Count; slot++)
            {
                if (!DigitPressed(keyboard, slot + 4))
                    continue;

                var worker = HudModel.Workers[slot];
                if (_fireRequestedId == worker.Id && Time.unscaledTime - _fireRequestedAt < ConfirmTime)
                {
                    StationCommands.FireWorker(worker.Id);
                    _fireRequestedId = -1;
                }
                else
                {
                    _fireRequestedId = worker.Id;
                    _fireRequestedAt = Time.unscaledTime;
                    HudModel.Notify(Loc.F("panel.staff.confirmFire", slot + 4, GameTexts.StaffName(worker.NameIndex)));
                }
            }
        }

        private static bool PlusPressed(Keyboard keyboard) =>
            keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame;

        private static bool MinusPressed(Keyboard keyboard) =>
            keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;

        private static bool DigitPressed(Keyboard keyboard, int digit) => digit switch
        {
            1 => keyboard.digit1Key.wasPressedThisFrame,
            2 => keyboard.digit2Key.wasPressedThisFrame,
            3 => keyboard.digit3Key.wasPressedThisFrame,
            4 => keyboard.digit4Key.wasPressedThisFrame,
            5 => keyboard.digit5Key.wasPressedThisFrame,
            6 => keyboard.digit6Key.wasPressedThisFrame,
            7 => keyboard.digit7Key.wasPressedThisFrame,
            8 => keyboard.digit8Key.wasPressedThisFrame,
            9 => keyboard.digit9Key.wasPressedThisFrame,
            _ => false
        };

        private static int UpgradePages() => (UpgradeMath.Purchasable.Length + UpgradesPerPage - 1) / UpgradesPerPage;

        // ---------------------------------------------------------------- panels

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
            }

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
                _builder.AppendLine();
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

        private void UpdateCards()
        {
            _cardTexts.Clear();
            foreach (var card in HudModel.Cards)
            {
                string who = card.Customer == CustomerType.Regular ? string.Empty : GameTexts.CustomerName(card.Customer) + "\n";
                _cardTexts.Add(card.State == CarState.Fueling
                    ? Loc.F("card.fueling", who, GameTexts.FuelName(card.Fuel), card.ReceivedLiters, card.RequestedLiters)
                    : card.State == CarState.WaitingForTires
                        ? Loc.F("card.tires", who)
                        : Loc.F("card.wants", who, GameTexts.FuelName(card.Fuel), card.RequestedLiters));
            }

            _view.SetCards(HudModel.Cards, _cardTexts);
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
                ? Loc.F("panel.finance.rating", Stars(Mathf.RoundToInt(rating)), rating, stats.RatingCount)
                : Loc.T("panel.finance.noRating"));

            if (HudModel.Reviews.Count > 0)
            {
                _builder.AppendLine(Loc.T("panel.finance.reviews"));
                for (int i = 0; i < HudModel.Reviews.Count && i < 4; i++)
                {
                    var review = HudModel.Reviews[i];
                    _builder.AppendLine($"{Stars(review.Stars)}  {Loc.T($"review.{review.Stars}.{review.Variant}")}");
                }
            }

            _builder.Append(HudModel.History.Count > 0 ? Loc.T("panel.finance.chart") : Loc.T("panel.finance.noHistory"));
            return _builder.ToString();
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

        private static string Stars(int stars) =>
            new string('★', Mathf.Clamp(stars, 0, 5)) + new string('☆', 5 - Mathf.Clamp(stars, 0, 5));

        private static int CountBits(ulong mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        private string BuildQuest()
        {
            var quest = HudModel.Quest;
            _builder.Clear();
            _builder.AppendLine(Loc.F("quest.header", GameTexts.QuestTitle(quest)));
            _builder.AppendLine(GameTexts.QuestGoalText(quest, HudModel.QuestProgress));
            _builder.Append(Loc.F("quest.reward", GameTexts.QuestReward(quest)));
            return _builder.ToString();
        }

        // ---------------------------------------------------------------- status blocks

        private string BuildStatus()
        {
            var economy = HudModel.Economy;
            int hours = (int)HudModel.Hour;
            int minutes = (int)((HudModel.Hour - hours) * 60f);

            _builder.Clear();
            _builder.AppendLine(StationProfile.DisplayName);
            _builder.AppendLine(Loc.F("hud.day", HudModel.Day, hours, minutes) + Loc.F("hud.speed", GameSettings.GameSpeed));
            _builder.AppendLine(Loc.F("hud.money", economy.Money));
            _builder.AppendLine(Loc.F("hud.reputation", economy.Reputation * 100f));
            _builder.AppendLine(Loc.F("hud.cleanliness", HudModel.Cleanliness.Value * 100f, HudModel.Cleanliness.TrashCount));
            if (HudModel.Workers.Count > 0)
            {
                float wages = 0f;
                foreach (var worker in HudModel.Workers)
                    wages += worker.Wage;
                _builder.AppendLine(Loc.F("hud.staff", HudModel.Workers.Count, wages));
            }

            var level = HudModel.Level;
            _builder.AppendLine(level.Level >= ProgressMath.MaxLevel
                ? Loc.F("hud.level.max", level.Level)
                : Loc.F("hud.level", level.Level, level.Experience, ProgressMath.ExperienceToNext(level.Level)));

            if (HudModel.World.Active != WorldEventKind.None)
                _builder.AppendLine(Loc.F("hud.event", Loc.T($"event.{HudModel.World.Active}"), HudModel.World.HoursLeft));

            _builder.AppendLine(Loc.F("hud.today", economy.DayIncome, economy.DayExpenses));
            _builder.Append(Loc.F("hud.served", economy.DayServed, economy.DayLost));
            return _builder.ToString();
        }

        private string BuildFuel()
        {
            _builder.Clear();
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = HudModel.Fuel[i];
                if ((int)_selectedFuel == i)
                    _builder.Append("▶ ");
                _builder.Append(Loc.F("hud.fuel", GameTexts.FuelName((FuelType)i), fuel.Amount, fuel.Capacity, fuel.SellPrice, fuel.MarketPrice));
                if (HudModel.PendingDelivery[i] > 0f)
                    _builder.Append(Loc.F("hud.fuel.pending", HudModel.PendingDelivery[i]));
                if (i < FuelTypes.Count - 1)
                    _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private string BuildPumps()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("hud.queue", HudModel.QueueLength, HudModel.CarsOnSite));

            if (HudModel.HasWash)
            {
                string wash = HudModel.Upgrades.CarWash == 0 ? Loc.T("hud.wash.closed")
                    : HudModel.WashTimeLeft > 0f ? Loc.F("hud.wash.working", HudModel.WashTimeLeft)
                    : HudModel.WashBusy ? Loc.T("hud.wash.arriving")
                    : Loc.T("hud.free.f");
                _builder.AppendLine(Loc.F("hud.wash", wash));
            }

            if (HudModel.HasParking)
            {
                _builder.AppendLine(HudModel.ParkingOpen == 0
                    ? Loc.T("hud.parking.closed")
                    : Loc.F("hud.parking", HudModel.ParkingUsed, HudModel.ParkingOpen));
            }

            if (HudModel.HasTireService)
            {
                string tires = HudModel.Upgrades.TireService == 0 ? Loc.T("hud.tires.closed")
                    : HudModel.TireTimeLeft > 0f ? Loc.F("hud.tires.working", HudModel.TireTimeLeft)
                    : HudModel.TireCarWaiting ? Loc.T("hud.tires.waiting")
                    : Loc.T("hud.free.m");
                _builder.AppendLine(Loc.F("hud.tires", tires));
            }

            if (HudModel.HasMotel)
            {
                if (HudModel.MotelOpen == 0)
                {
                    _builder.AppendLine(Loc.T("hud.motel.closed"));
                }
                else
                {
                    _builder.Append(Loc.F("hud.motel", HudModel.MotelUsed, HudModel.MotelOpen));
                    if (HudModel.MotelDirty > 0)
                        _builder.Append(Loc.F("hud.motel.dirty", HudModel.MotelDirty));
                    _builder.AppendLine();
                }
            }

            if (HudModel.HasRestroom)
            {
                _builder.AppendLine(HudModel.RestroomDirt >= FacilityMath.RestroomDisgustingDirt
                    ? Loc.T("hud.restroom.disgusting")
                    : Loc.F("hud.restroom", HudModel.RestroomDirt * 100f));
            }

            foreach (var pump in HudModel.Pumps)
            {
                _builder.Append(Loc.F("hud.pump", pump.Number));
                if (pump.Locked)
                {
                    _builder.AppendLine(Loc.T("hud.pump.locked"));
                    continue;
                }

                if (pump.Condition <= 0f && !pump.Occupied)
                {
                    _builder.AppendLine(Loc.T("hud.pump.broken"));
                    continue;
                }

                string wear = pump.Condition < ProgressMath.RepairThreshold
                    ? Loc.F("hud.pump.wear", 100f - pump.Condition * 100f)
                    : string.Empty;
                if (!pump.Occupied)
                {
                    _builder.AppendLine(Loc.T("hud.free.f") + wear);
                    continue;
                }

                _builder.AppendLine(Loc.F("hud.pump.car", GameTexts.CustomerName(pump.Customer), GameTexts.CarStateName(pump.CarState),
                    GameTexts.FuelName(pump.FuelType), pump.ReceivedLiters, pump.RequestedLiters, pump.PatienceRatio * 100f, wear));
            }

            return _builder.ToString().TrimEnd();
        }

        private string BuildCenter()
        {
            var report = HudModel.LastReport;
            if (report.Day > 0 && report.Day != _shownReportDay)
            {
                _shownReportDay = report.Day;
                _reportShownAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - _reportShownAt < ReportDuration)
            {
                return Loc.F("hud.report", report.Day, report.Income, report.Expenses, report.Income - report.Expenses,
                    report.Served, report.Lost);
            }

            if (HudModel.Message != null && Time.unscaledTime - HudModel.MessageTime < MessageDuration)
                return HudModel.Message;

            return HudModel.Hint switch
            {
                InteractionHint.None => string.Empty,
                InteractionHint.PumpFree => string.Empty,
                InteractionHint.Repair => Loc.F("hint.Repair", ProgressMath.RepairStepCost),
                InteractionHint.Renovate => Loc.F("hint.Renovate", Loc.T($"renovation.{HudModel.NearRenovationKind}"),
                    RenovationMath.Cost(HudModel.NearRenovationKind), RenovationMath.RequiredLevel(HudModel.NearRenovationKind)),
                _ => Loc.T($"hint.{HudModel.Hint}")
            };
        }
    }
}
