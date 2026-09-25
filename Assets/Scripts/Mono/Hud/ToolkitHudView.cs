using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GasStation.Mono.Hud
{
    /// <summary>
    /// HUD on UI Toolkit with the styles from Resources/UI/Hud; the visual tree is built in code. Returns null from TryCreate when
    /// the assets are missing, so StationHud can fall back to uGUI.
    /// </summary>
    public class ToolkitHudView : IHudView
    {
        private const string StylePath = "UI/Hud";

        private readonly VisualElement _root;
        private readonly VisualElement[] _cards = new VisualElement[6];
        private readonly Label[] _labels = new Label[6];
        private readonly Label[] _meterLabels = new Label[3];
        private readonly VisualElement[] _meterFills = new VisualElement[3];
        private readonly VisualElement _meters;
        private readonly Label _marker;
        private readonly List<VisualElement> _cardPool = new();
        private readonly VisualElement _chart;
        private readonly List<VisualElement> _chartColumns = new();
        private readonly VisualElement _moneyLayer;
        private readonly List<Label> _moneyPool = new();

        public static IHudView TryCreate(GameObject host)
        {
            var style = Resources.Load<StyleSheet>(StylePath);
            if (style == null)
                return null;

            var document = UiToolkit.CreateDocument(host, "HUD (UI Toolkit)", 100);
            return document != null ? new ToolkitHudView(document.rootVisualElement, style) : null;
        }

        private ToolkitHudView(VisualElement root, StyleSheet style)
        {
            _root = root;
            _root.styleSheets.Add(style);
            _root.AddToClassList("hud-root");
            _root.pickingMode = PickingMode.Ignore;

            CreateCard(HudBlock.Status, "card--top-left", accent: true);
            CreateCard(HudBlock.Fuel, "card--top-right", textClass: "hud-text--right");
            CreateCard(HudBlock.Pumps, "card--bottom-left");
            CreateCard(HudBlock.Help, "card--bottom-right", textClass: "hud-text--small");
            CreateCard(HudBlock.Panel, "card--top-center", accent: true);
            CreateCard(HudBlock.Center, "card--center", textClass: "hud-text--center");

            _marker = new Label("▼") { pickingMode = PickingMode.Ignore };
            _marker.AddToClassList("quest-marker");
            _root.Add(_marker);

            _chart = new VisualElement { pickingMode = PickingMode.Ignore };
            _chart.AddToClassList("chart");
            _cards[(int)HudBlock.Panel].Add(_chart);

            _meters = new VisualElement();
            _cards[(int)HudBlock.Status].Add(_meters);
            CreateMeter(0, "meter__fill--reputation");
            CreateMeter(1, null);
            CreateMeter(2, "meter__fill--xp");

            _moneyLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _moneyLayer.AddToClassList("money-pop__layer");
            _root.Add(_moneyLayer);
        }

        public void SetVisible(bool visible) =>
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetText(HudBlock block, string text)
        {
            var card = _cards[(int)block];
            bool visible = !string.IsNullOrEmpty(text);
            card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;

            var label = _labels[(int)block];
            if (label.text != text)
                label.text = text;

            // Long panels (staff, shop, upgrades) get a wider card.
            if (block == HudBlock.Panel)
                card.EnableInClassList("card--wide", text.Length > 220);
        }

        public void SetMeters(HudMeters meters)
        {
            SetMeter(0, Loc.T("meter.reputation"), meters.Reputation);
            SetMeter(1, Loc.T("meter.cleanliness"), meters.Cleanliness);
            SetMeter(2, Loc.T("meter.experience"), meters.Experience);
        }

        public void SetMarker(bool visible, Vector3 worldPosition)
        {
            var camera = Camera.main;
            bool onScreen = visible && camera != null &&
                            camera.WorldToViewportPoint(worldPosition).z > 0f && _root.panel != null;
            _marker.style.display = onScreen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!onScreen)
                return;

            var point = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPosition + Vector3.up * 3f, camera);
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 8f;
            _marker.style.left = point.x - 20f;
            _marker.style.top = point.y - 56f + bob;
        }

        public void SetCards(IReadOnlyList<CarCard> cards, IReadOnlyList<string> texts)
        {
            var camera = Camera.main;
            int shown = 0;
            if (camera != null && _root.panel != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    if (camera.WorldToViewportPoint(card.Position).z <= 0f)
                        continue;

                    var element = CardAt(shown++);
                    element.style.display = DisplayStyle.Flex;
                    var point = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, card.Position + Vector3.up * 2.4f, camera);
                    element.style.left = point.x - 70f;
                    element.style.top = point.y - 44f;

                    var label = (Label)element[0];
                    if (label.text != texts[i])
                        label.text = texts[i];

                    var fill = element[1][0];
                    float patience = Mathf.Clamp01(card.PatienceRatio);
                    fill.style.width = Length.Percent(patience * 100f);
                    fill.style.backgroundColor = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.3f, 0.85f, 0.4f), patience);
                }
            }

            for (int i = shown; i < _cardPool.Count; i++)
                _cardPool[i].style.display = DisplayStyle.None;
        }

        public void SetChart(IReadOnlyList<DayHistoryEntry> history)
        {
            bool visible = history != null && history.Count > 0;
            _chart.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;

            const int days = 14;
            int first = Mathf.Max(0, history.Count - days);
            float max = 1f;
            for (int i = first; i < history.Count; i++)
                max = Mathf.Max(max, Mathf.Max(history[i].Income, history[i].Expenses));

            int count = history.Count - first;
            while (_chartColumns.Count < count)
                _chartColumns.Add(CreateColumn());

            for (int i = 0; i < _chartColumns.Count; i++)
            {
                var column = _chartColumns[i];
                if (i >= count)
                {
                    column.style.display = DisplayStyle.None;
                    continue;
                }

                var entry = history[first + i];
                column.style.display = DisplayStyle.Flex;
                column[0][0].style.height = Length.Percent(entry.Income / max * 100f);
                column[0][1].style.height = Length.Percent(entry.Expenses / max * 100f);
                ((Label)column[1]).text = entry.Day.ToString();
            }
        }

        public void SetMoneyPopups(IReadOnlyList<MoneyPopup> popups)
        {
            while (_moneyPool.Count < popups.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("money-pop");
                _moneyLayer.Add(label);
                _moneyPool.Add(label);
            }

            for (int i = 0; i < _moneyPool.Count; i++)
            {
                var label = _moneyPool[i];
                if (i >= popups.Count)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var popup = popups[i];
                float age = Time.time - popup.BornAt;
                label.style.display = DisplayStyle.Flex;
                if (label.text != popup.Text)
                    label.text = popup.Text;
                label.EnableInClassList("money-pop--expense", !popup.Income);
                label.style.translate = new Translate(0f, -age * 46f);
                label.style.opacity = Mathf.Clamp01(1f - age / 1.4f);
            }
        }

        private VisualElement CreateColumn()
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.AddToClassList("chart__column");

            var bars = new VisualElement { pickingMode = PickingMode.Ignore };
            bars.AddToClassList("chart__bars");
            var income = new VisualElement { pickingMode = PickingMode.Ignore };
            income.AddToClassList("chart__bar");
            income.AddToClassList("chart__bar--income");
            var expense = new VisualElement { pickingMode = PickingMode.Ignore };
            expense.AddToClassList("chart__bar");
            expense.AddToClassList("chart__bar--expense");
            bars.Add(income);
            bars.Add(expense);
            column.Add(bars);

            var day = new Label { pickingMode = PickingMode.Ignore };
            day.AddToClassList("chart__day");
            column.Add(day);

            _chart.Add(column);
            return column;
        }

        private VisualElement CardAt(int index)
        {
            while (_cardPool.Count <= index)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList("car-card");
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("car-card__text");
                card.Add(label);

                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.AddToClassList("car-card__track");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("car-card__fill");
                track.Add(fill);
                card.Add(track);

                // Cards sit under the HUD cards so panels stay readable.
                _root.Insert(0, card);
                _cardPool.Add(card);
            }

            return _cardPool[index];
        }

        private void SetMeter(int index, string label, float value)
        {
            if (_meterLabels[index].text != label)
                _meterLabels[index].text = label;
            _meterFills[index].style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }

        private void CreateCard(HudBlock block, string positionClass, bool accent = false, string textClass = null)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("card");
            card.AddToClassList(positionClass);

            if (accent)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("accent-line");
                card.Add(line);
            }

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("hud-text");
            if (textClass != null)
                label.AddToClassList(textClass);
            card.Add(label);

            _root.Add(card);
            _cards[(int)block] = card;
            _labels[(int)block] = label;
        }

        private void CreateMeter(int index, string fillClass)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("meter");

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("meter__label");
            row.Add(label);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("meter__track");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("meter__fill");
            if (fillClass != null)
                fill.AddToClassList(fillClass);
            track.Add(fill);
            row.Add(track);

            _meters.Add(row);
            _meterLabels[index] = label;
            _meterFills[index] = fill;
        }
    }
}
