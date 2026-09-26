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
    public partial class StationHud : MonoBehaviour
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
        private bool _regularsOpen;
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
        private const float MoneyPopupLifetime = 1.4f;
        private readonly System.Collections.Generic.List<MoneyPopup> _moneyPopups = new();

        private void Awake()
        {
            _view = ToolkitHudView.TryCreate(gameObject) ?? new UguiHudView(gameObject);
        }

        private void Update()
        {
            _view.SetVisible(HudModel.HasStation && !LaptopState.IsOpen && !PhotoMode.Active && !GamePause.MenuOpen);
            if (!HudModel.HasStation)
                return;

            if (!GamePause.MenuOpen && !BuildMode.Active && !PhotoMode.Active)
                HandleKeys();
            _view.SetText(HudBlock.Status, BuildStatus());
            _view.SetText(HudBlock.Fuel, BuildFuel());
            _view.SetText(HudBlock.Pumps, BuildPumps());
            _view.SetCrosshair(!BuildMode.Active);
            _view.SetText(HudBlock.Center, BuildCenter());
            _view.SetText(HudBlock.Help, GameSettings.ShowControls ? Loc.T("hud.help") : string.Empty);
            _view.SetText(HudBlock.Panel, BuildMode.Active ? BuildBuildMode()
                : _upgradesOpen ? BuildUpgrades()
                : _storeOpen ? BuildStore()
                : _paintOpen ? BuildPaint()
                : _staffOpen ? BuildStaff()
                : _achievementsOpen ? BuildAchievements()
                : _regularsOpen ? BuildRegulars()
                : _financeOpen ? _financePage switch { 1 => BuildBank(), 2 => BuildCompetitor(), _ => BuildFinance() }
                : BuildQuest());
            _view.SetChart(_financeOpen && _financePage == 0 && !BuildMode.Active ? HudModel.History : null);
            UpdateCards();
            UpdateMoneyPopups();

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
            if (keyboard.kKey.wasPressedThisFrame)
                Toggle(ref _regularsOpen);
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
            _regularsOpen = false;
        }

        private void HandleFuelKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol92;
            if (keyboard.digit2Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol95;
            if (keyboard.digit3Key.wasPressedThisFrame) _selectedFuel = FuelType.Diesel;

            if (Keys.PlusPressed(keyboard))
                StationCommands.ChangePrice(_selectedFuel, PriceStep);
            if (Keys.MinusPressed(keyboard))
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
                if (Keys.DigitPressed(keyboard, i + 1))
                    _selectedProduct = (ProductType)i;
            }

            if (Keys.PlusPressed(keyboard))
                StationCommands.ChangeProductPrice(_selectedProduct, ProductPriceStep);
            if (Keys.MinusPressed(keyboard))
                StationCommands.ChangeProductPrice(_selectedProduct, -ProductPriceStep);
            if (keyboard.oKey.wasPressedThisFrame)
                StationCommands.OrderProducts(_selectedProduct, ShopMath.OrderSize);
            if (keyboard.pKey.wasPressedThisFrame)
                StationCommands.TogglePromo(_selectedProduct);
            if (keyboard.iKey.wasPressedThisFrame && HudModel.HasDiner)
                StationCommands.OrderIngredients();
        }

        private void HandleUpgradeKeys(Keyboard keyboard)
        {
            for (int slot = 0; slot < UpgradesPerPage; slot++)
            {
                int index = _upgradePage * UpgradesPerPage + slot;
                if (index < UpgradeMath.Purchasable.Length && Keys.DigitPressed(keyboard, slot + 1))
                    StationCommands.BuyUpgrade(UpgradeMath.Purchasable[index]);
            }
        }

        private void HandlePaintKeys(Keyboard keyboard)
        {
            for (int scheme = 1; scheme < StyleMath.SchemeCount; scheme++)
            {
                if (Keys.DigitPressed(keyboard, scheme))
                    StationCommands.PaintStation(scheme);
            }
        }

        private void HandleStaffKeys(Keyboard keyboard)
        {
            for (int i = 0; i < StaffMath.CandidatesPerDay; i++)
            {
                if (Keys.DigitPressed(keyboard, i + 1))
                    StationCommands.HireCandidate(i);
            }

            // Workers are listed as 4..9; firing needs a second press within two seconds.
            for (int slot = 0; slot < StaffMath.MaxStaff && slot < HudModel.Workers.Count; slot++)
            {
                if (!Keys.DigitPressed(keyboard, slot + 4))
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

        private static int UpgradePages() => (UpgradeMath.Purchasable.Length + UpgradesPerPage - 1) / UpgradesPerPage;

        // ---------------------------------------------------------------- car cards (panels: StationHud.Panels.cs, status: StationHud.Status.cs)

        // ---------------------------------------------------------------- money popups

        /// <summary>Floating "+$12"/"-$300" by the money panel, from this frame's station events.</summary>
        private void UpdateMoneyPopups()
        {
            for (int i = _moneyPopups.Count - 1; i >= 0; i--)
            {
                if (Time.time - _moneyPopups[i].BornAt > MoneyPopupLifetime)
                    _moneyPopups.RemoveAt(i);
            }

            foreach (var stationEvent in HudModel.Events)
            {
                bool income;
                switch (stationEvent.Type)
                {
                    case StationEventType.CustomerPaid:
                    case StationEventType.TipReceived:
                    case StationEventType.ShopSale:
                    case StationEventType.CarWashed:
                    case StationEventType.ParkingPaid:
                    case StationEventType.MotelPaid:
                    case StationEventType.TiresChanged:
                    case StationEventType.DinerSale:
                    case StationEventType.EvCharged:
                    case StationEventType.QuestCompleted:
                    case StationEventType.HostedEventEnded:
                        income = true;
                        break;
                    case StationEventType.UpgradeBought:
                    case StationEventType.UtilitiesPaid:
                    case StationEventType.TaxPaid:
                    case StationEventType.InsurancePremiumPaid:
                    case StationEventType.LoanPayment:
                    case StationEventType.ContractPenalty:
                    case StationEventType.Robbery:
                    case StationEventType.GoodsStolen:
                        income = false;
                        break;
                    default:
                        continue;
                }

                if (stationEvent.Value <= 0f)
                    continue;

                _moneyPopups.Add(new MoneyPopup
                {
                    Text = (income ? "+$" : "-$") + Mathf.RoundToInt(stationEvent.Value),
                    Income = income,
                    BornAt = Time.time
                });
                if (_moneyPopups.Count > 12)
                    _moneyPopups.RemoveAt(0);
            }

            _view.SetMoneyPopups(_moneyPopups);
        }

        private void UpdateCards()
        {
            _cardTexts.Clear();
            foreach (var card in HudModel.Cards)
            {
                // Regulars are shown by name; the critic is incognito and looks like anyone else.
                string who = card.ContractId > 0 ? ContractLabel(card.ContractId) + "\n"
                    : card.RegularId > 0 ? GameTexts.RegularName(card.RegularId) + "\n"
                    : card.Customer is CustomerType.Regular or CustomerType.Critic ? string.Empty
                    : GameTexts.CustomerName(card.Customer) + "\n";
                _cardTexts.Add(card.State == CarState.Fueling
                    ? Loc.F("card.fueling", who, GameTexts.FuelName(card.Fuel), card.ReceivedLiters, card.RequestedLiters)
                    : card.State == CarState.WaitingForTires
                        ? Loc.F("card.tires", who)
                        : Loc.F("card.wants", who, GameTexts.FuelName(card.Fuel), card.RequestedLiters));
            }

            _view.SetCards(HudModel.Cards, _cardTexts);
        }

        private static string ContractLabel(int contractId)
        {
            foreach (var contract in HudModel.Contracts)
            {
                if (contract.Id == contractId)
                    return Loc.F("card.contract", GameTexts.ContractName(contract.Type));
            }

            return Loc.F("card.contract", string.Empty);
        }

    }
}
