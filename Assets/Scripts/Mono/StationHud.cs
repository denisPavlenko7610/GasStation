using System.Text;
using GasStation.Bridge;
using GasStation.Components;
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

        private static readonly string[] FuelNames = { "АИ-92", "АИ-95", "ДТ" };

        private readonly StringBuilder _builder = new();
        private Canvas _canvas;
        private Text _status;
        private Text _fuel;
        private Text _pumps;
        private Text _center;
        private Text _help;
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
            _help.text = "WASD — ходить   E / ЛКМ — заправить\n1/2/3 — выбрать топливо   +/- — цена   O — заказать 500 л";
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
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

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

        private string BuildStatus()
        {
            var economy = HudModel.Economy;
            int hours = (int)HudModel.Hour;
            int minutes = (int)((HudModel.Hour - hours) * 60f);

            _builder.Clear();
            _builder.AppendLine($"День {HudModel.Day}   {hours:00}:{minutes:00}");
            _builder.AppendLine($"Деньги: ${economy.Money:0}");
            _builder.AppendLine($"Репутация: {economy.Reputation * 100f:0}%");
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
                _builder.Append($"{marker}{FuelNames[i]}: {fuel.Amount:0}/{fuel.Capacity:0} л   ${fuel.SellPrice:0.00} (рынок ${fuel.MarketPrice:0.00})");
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
            foreach (var pump in HudModel.Pumps)
            {
                _builder.Append($"Колонка {pump.Number}: ");
                if (!pump.Occupied)
                {
                    _builder.AppendLine("свободна");
                    continue;
                }

                string fuel = FuelNames[(int)pump.FuelType];
                string state = pump.CarState switch
                {
                    CarState.DrivingToPump => "подъезжает",
                    CarState.WaitingForService => "ждёт заправки",
                    CarState.Fueling => "заправляется",
                    _ => pump.CarState.ToString()
                };
                _builder.AppendLine($"{state}, {fuel} {pump.ReceivedLiters:0}/{pump.RequestedLiters:0} л, терпение {pump.PatienceRatio * 100f:0}%");
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
