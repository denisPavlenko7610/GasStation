using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GasStation.Mono
{
    /// <summary>Minimal text HUD built at runtime. Reads HudModel and sends StationCommands.</summary>
    public class StationHud : MonoBehaviour
    {
        private const float PriceStep = 0.05f;
        private const float OrderLiters = 500f;
        private const float MessageDuration = 3f;
        private const float ReportDuration = 8f;

        private const float NewGameConfirmTime = 2f;
        private const float ProductPriceStep = 0.25f;

        private readonly StringBuilder _builder = new();
        private Canvas _canvas;
        private Text _status;
        private Text _fuel;
        private Text _pumps;
        private Text _center;
        private Text _help;
        private Text _shop;
        private const int UpgradesPerPage = 6;
        private bool _upgradesOpen;
        private int _upgradePage;
        private bool _storeOpen;
        private ProductType _selectedProduct;
        private float _newGameRequestedAt = float.NegativeInfinity;
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
            _status = CreateText("Status", font, new Vector2(0f, 1f), TextAnchor.UpperLeft, 28);
            _fuel = CreateText("Fuel", font, new Vector2(1f, 1f), TextAnchor.UpperRight, 26);
            _pumps = CreateText("Pumps", font, new Vector2(0f, 0f), TextAnchor.LowerLeft, 26);
            _center = CreateText("Center", font, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, 34);
            _help = CreateText("Help", font, new Vector2(1f, 0f), TextAnchor.LowerRight, 22);
            _help.text = "WASD — ходить   E / ЛКМ — заправить / убрать мусор\n1/2/3 — топливо   +/- — цена   O — заказать 500 л\n" +
                         "Tab — улучшения   M — магазин   F5 — сохранить   F9 — загрузить   F10 ×2 — новая игра";
            _shop = CreateText("Shop", font, new Vector2(0.5f, 1f), TextAnchor.UpperCenter, 26);
        }

        private void Update()
        {
            _canvas.enabled = HudModel.HasStation;
            if (!HudModel.HasStation)
                return;

            HandleKeys();
            _status.text = BuildStatus();
            _fuel.text = BuildFuel();
            _pumps.text = BuildPumps();
            _center.text = BuildCenter();
            _shop.text = _upgradesOpen ? BuildUpgrades() : _storeOpen ? BuildStore() : BuildQuest();
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                // Tab: page 1 → page 2 → closed.
                int pages = (UpgradeTypes.Count + UpgradesPerPage - 1) / UpgradesPerPage;
                if (!_upgradesOpen)
                {
                    _upgradesOpen = true;
                    _upgradePage = 0;
                }
                else if (++_upgradePage >= pages)
                {
                    _upgradesOpen = false;
                }

                _storeOpen = false;
            }

            if (keyboard.mKey.wasPressedThisFrame && HudModel.HasShop)
            {
                _storeOpen = !_storeOpen;
                _upgradesOpen = false;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
                StationCommands.SaveGame();
            if (keyboard.f9Key.wasPressedThisFrame)
                StationCommands.LoadGame();
            if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (Time.unscaledTime - _newGameRequestedAt < NewGameConfirmTime)
                {
                    StationCommands.NewGame();
                    _newGameRequestedAt = float.NegativeInfinity;
                }
                else
                {
                    _newGameRequestedAt = Time.unscaledTime;
                    HudModel.Notify("Нажмите F10 ещё раз, чтобы начать заново");
                }
            }

            if (_upgradesOpen)
            {
                for (int slot = 0; slot < UpgradesPerPage; slot++)
                {
                    int index = _upgradePage * UpgradesPerPage + slot;
                    if (index < UpgradeTypes.Count && DigitPressed(keyboard, slot + 1))
                        StationCommands.BuyUpgrade((UpgradeType)index);
                }

                return;
            }

            if (_storeOpen)
            {
                for (int i = 0; i < ProductTypes.Count; i++)
                {
                    if (DigitPressed(keyboard, i + 1))
                        _selectedProduct = (ProductType)i;
                }

                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                    StationCommands.ChangeProductPrice(_selectedProduct, ProductPriceStep);
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                    StationCommands.ChangeProductPrice(_selectedProduct, -ProductPriceStep);
                if (keyboard.oKey.wasPressedThisFrame)
                    StationCommands.OrderProducts(_selectedProduct, ShopMath.OrderSize);
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol92;
            if (keyboard.digit2Key.wasPressedThisFrame) _selectedFuel = FuelType.Petrol95;
            if (keyboard.digit3Key.wasPressedThisFrame) _selectedFuel = FuelType.Diesel;

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                StationCommands.ChangePrice(_selectedFuel, PriceStep);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                StationCommands.ChangePrice(_selectedFuel, -PriceStep);
            if (keyboard.oKey.wasPressedThisFrame)
                StationCommands.OrderFuel(_selectedFuel, OrderLiters);
        }

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

        private string BuildUpgrades()
        {
            _builder.Clear();
            int pages = (UpgradeTypes.Count + UpgradesPerPage - 1) / UpgradesPerPage;
            _builder.AppendLine($"УЛУЧШЕНИЯ, стр. {_upgradePage + 1}/{pages} (цифра — купить, Tab — дальше / закрыть)");
            for (int slot = 0; slot < UpgradesPerPage; slot++)
            {
                int i = _upgradePage * UpgradesPerPage + slot;
                if (i >= UpgradeTypes.Count)
                    break;

                var type = (UpgradeType)i;
                int level = HudModel.Upgrades.Get(type);
                int max = UpgradeMath.MaxLevel(type);
                int requiredLevel = ProgressMath.RequiredLevel(type, level);
                string price = !UpgradeMath.CanUpgrade(type, level) ? "макс."
                    : HudModel.Level.Level < requiredLevel ? $"нужен уровень {requiredLevel}"
                    : $"${UpgradeMath.Cost(type, level):0}";
                _builder.AppendLine($"{slot + 1}. {GameTexts.UpgradeName(type)} [{level}/{max}] — {price}: {GameTexts.UpgradeDescription(type)}");
            }

            return _builder.ToString();
        }

        private string BuildStore()
        {
            _builder.Clear();
            _builder.AppendLine($"МАГАЗИН (1–5 — товар, +/- — цена, O — заказать {ShopMath.OrderSize} шт., M — закрыть)");
            _builder.AppendLine($"Покупателей внутри: {HudModel.PedestriansInShop}");
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                var product = HudModel.Products[i];
                string marker = (int)_selectedProduct == i ? "▶ " : "";
                _builder.Append($"{marker}{i + 1}. {GameTexts.ProductName((ProductType)i)}: {product.Stock}/{product.Capacity}   " +
                                $"${product.SellPrice:0.00} (закупка ${product.BuyPrice:0.00}, обычно ${product.ReferencePrice:0.00})");
                if (HudModel.PendingProducts[i] > 0)
                    _builder.Append($"   +{HudModel.PendingProducts[i]} в пути");
                _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private string BuildQuest()
        {
            var quest = HudModel.Quest;
            string title = quest.IsDaily ? "Задание дня" : $"Задание: {GameTexts.QuestTitle(quest)}";
            string goal = string.Format(GameTexts.QuestGoalText(quest), HudModel.QuestProgress.ToString("0"));
            return $"{title}\n{goal}\nНаграда: {GameTexts.QuestReward(quest)}";
        }

        private string BuildStatus()
        {
            var economy = HudModel.Economy;
            int hours = (int)HudModel.Hour;
            int minutes = (int)((HudModel.Hour - hours) * 60f);

            _builder.Clear();
            _builder.AppendLine($"День {HudModel.Day}   {hours:00}:{minutes:00}");
            _builder.AppendLine($"Деньги: ${economy.Money:0}");
            _builder.AppendLine($"Репутация: {economy.Reputation * 100f:0}%");
            _builder.AppendLine($"Чистота: {HudModel.Cleanliness.Value * 100f:0}% (мусора: {HudModel.Cleanliness.TrashCount})");
            var level = HudModel.Level;
            _builder.AppendLine(level.Level >= ProgressMath.MaxLevel
                ? $"Уровень станции: {level.Level} (макс.)"
                : $"Уровень станции: {level.Level}   опыт {level.Experience:0}/{ProgressMath.ExperienceToNext(level.Level):0}");
            string worldEvent = HudModel.World.Active switch
            {
                WorldEventKind.RushHour => "Час пик",
                WorldEventKind.Sandstorm => "Песчаная буря",
                _ => null
            };
            if (worldEvent != null)
                _builder.AppendLine($"Событие: {worldEvent} (ещё {HudModel.World.HoursLeft:0.0} ч)");
            _builder.AppendLine($"Сегодня: +${economy.DayIncome:0} / -${economy.DayExpenses:0}");
            _builder.Append($"Обслужено: {economy.DayServed}   Уехали: {economy.DayLost}");
            return _builder.ToString();
        }

        private string BuildFuel()
        {
            _builder.Clear();
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = HudModel.Fuel[i];
                string marker = (int)_selectedFuel == i ? "▶ " : "";
                _builder.Append($"{marker}{GameTexts.FuelName((FuelType)i)}: {fuel.Amount:0}/{fuel.Capacity:0} л   ${fuel.SellPrice:0.00} (рынок ${fuel.MarketPrice:0.00})");
                if (HudModel.PendingDelivery[i] > 0f)
                    _builder.Append($"   +{HudModel.PendingDelivery[i]:0} л в пути");
                _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private string BuildPumps()
        {
            _builder.Clear();
            _builder.AppendLine($"Очередь: {HudModel.QueueLength}   Машин на станции: {HudModel.CarsOnSite}");
            if (HudModel.HasWash)
            {
                string wash = HudModel.Upgrades.CarWash == 0 ? "закрыта (улучшение «Автомойка»)"
                    : HudModel.WashTimeLeft > 0f ? $"моет машину, ещё {HudModel.WashTimeLeft:0} с"
                    : HudModel.WashBusy ? "машина подъезжает"
                    : "свободна";
                _builder.AppendLine($"Мойка: {wash}");
            }

            if (HudModel.HasParking)
            {
                _builder.AppendLine(HudModel.ParkingOpen == 0
                    ? "Стоянка для фур: закрыта (улучшение «Стоянка для фур»)"
                    : $"Стоянка для фур: {HudModel.ParkingUsed}/{HudModel.ParkingOpen} мест занято");
            }

            if (HudModel.HasRestroom)
                _builder.AppendLine($"Туалет: {(HudModel.RestroomDirt >= FacilityMath.RestroomDisgustingDirt ? "ОТВРАТИТЕЛЬНО" : $"грязь {HudModel.RestroomDirt * 100f:0}%")}");
            foreach (var pump in HudModel.Pumps)
            {
                _builder.Append($"Колонка {pump.Number}: ");
                if (pump.Locked)
                {
                    _builder.AppendLine("закрыта (улучшение «Новая колонка»)");
                    continue;
                }

                if (pump.Condition <= 0f && !pump.Occupied)
                {
                    _builder.AppendLine("СЛОМАНА — почините (E рядом)");
                    continue;
                }

                string wear = pump.Condition < ProgressMath.RepairThreshold ? $" [износ {100f - pump.Condition * 100f:0}%]" : string.Empty;
                if (!pump.Occupied)
                {
                    _builder.AppendLine($"свободна{wear}");
                    continue;
                }

                string fuel = GameTexts.FuelName(pump.FuelType);
                string state = pump.CarState switch
                {
                    CarState.DrivingToPump => "подъезжает",
                    CarState.WaitingForService => "ждёт заправки",
                    CarState.Fueling => "заправляется",
                    CarState.Shopping => "водитель в магазине",
                    CarState.ReadyToLeave => "уезжает",
                    _ => pump.CarState.ToString()
                };
                _builder.AppendLine($"{GameTexts.CustomerName(pump.Customer)} {state}, {fuel} {pump.ReceivedLiters:0}/{pump.RequestedLiters:0} л, " +
                                    $"терпение {pump.PatienceRatio * 100f:0}%{wear}");
            }

            return _builder.ToString();
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
                return $"Итоги дня {report.Day}\nДоход: ${report.Income:0}   Расходы: ${report.Expenses:0}\n" +
                       $"Прибыль: ${report.Income - report.Expenses:0}\nОбслужено: {report.Served}   Уехали: {report.Lost}";
            }

            if (HudModel.Message != null && Time.unscaledTime - HudModel.MessageTime < MessageDuration)
                return HudModel.Message;

            return HudModel.Hint switch
            {
                InteractionHint.CanStartFueling => "[E / ЛКМ] Заправить",
                InteractionHint.Fueling => "Идёт заправка...",
                InteractionHint.CarArriving => "Машина подъезжает",
                InteractionHint.Trash => "[E / ЛКМ] Убрать мусор",
                InteractionHint.Restroom => "[E / ЛКМ] Убрать туалет",
                InteractionHint.Repair => $"[E / ЛКМ] Чинить колонку (${ProgressMath.RepairStepCost:0} за шаг)",
                _ => string.Empty
            };
        }

        private Text CreateText(string name, Font font, Vector2 anchor, TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(anchor.x < 0.5f ? 24f : anchor.x > 0.5f ? -24f : 0f,
                anchor.y < 0.5f ? 24f : anchor.y > 0.5f ? -24f : 0f);
            rect.sizeDelta = new Vector2(900f, 400f);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }
    }
}
