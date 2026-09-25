using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GasStation.Mono
{
    /// <summary>
    /// HUD built at runtime from uGUI: text blocks on semi-transparent panels. Reads HudModel, sends
    /// StationCommands and takes all strings from Loc (Unity Localization).
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

        private static readonly Color PanelColor = new(0.05f, 0.06f, 0.08f, 0.62f);

        private readonly StringBuilder _builder = new();
        private Canvas _canvas;
        private Text _status;
        private Text _fuel;
        private Text _pumps;
        private Text _center;
        private Text _help;
        private Text _panel;

        private bool _upgradesOpen;
        private int _upgradePage;
        private bool _storeOpen;
        private bool _paintOpen;
        private bool _staffOpen;
        private int _fireRequestedId = -1;
        private float _fireRequestedAt = float.NegativeInfinity;
        private float _newGameRequestedAt = float.NegativeInfinity;
        private ProductType _selectedProduct;
        private FuelType _selectedFuel;
        private int _shownReportDay;
        private float _reportShownAt = float.NegativeInfinity;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _status = CreateBlock("Status", font, new Vector2(0f, 1f), TextAnchor.UpperLeft, 26);
            _fuel = CreateBlock("Fuel", font, new Vector2(1f, 1f), TextAnchor.UpperRight, 24);
            _pumps = CreateBlock("Pumps", font, new Vector2(0f, 0f), TextAnchor.LowerLeft, 22);
            _center = CreateBlock("Center", font, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, 32);
            _help = CreateBlock("Help", font, new Vector2(1f, 0f), TextAnchor.LowerRight, 18);
            _panel = CreateBlock("Panel", font, new Vector2(0.5f, 1f), TextAnchor.UpperLeft, 24);
        }

        private void Update()
        {
            _canvas.enabled = HudModel.HasStation;
            if (!HudModel.HasStation)
                return;

            HandleKeys();
            SetText(_status, BuildStatus());
            SetText(_fuel, BuildFuel());
            SetText(_pumps, BuildPumps());
            SetText(_center, BuildCenter());
            SetText(_help, Loc.T("hud.help"));
            SetText(_panel, _upgradesOpen ? BuildUpgrades()
                : _storeOpen ? BuildStore()
                : _paintOpen ? BuildPaint()
                : _staffOpen ? BuildStaff()
                : BuildQuest());
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
                    StationCommands.NewGame();
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
            _builder.AppendLine(Loc.F("hud.day", HudModel.Day, hours, minutes));
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
                _ => Loc.T($"hint.{HudModel.Hint}")
            };
        }

        // ---------------------------------------------------------------- widgets

        private static void SetText(Text text, string value)
        {
            bool visible = !string.IsNullOrEmpty(value);
            var panel = text.transform.parent.gameObject;
            if (panel.activeSelf != visible)
                panel.SetActive(visible);
            if (visible && text.text != value)
                text.text = value;
        }

        /// <summary>A text block on a dark panel that grows with its content, anchored to a screen corner or edge.</summary>
        private Text CreateBlock(string name, Font font, Vector2 anchor, TextAnchor alignment, int fontSize)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(anchor.x < 0.5f ? 16f : anchor.x > 0.5f ? -16f : 0f,
                anchor.y < 0.5f ? 16f : anchor.y > 0.5f ? -16f : 0f);

            var background = panel.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(panel.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            return text;
        }
    }
}
