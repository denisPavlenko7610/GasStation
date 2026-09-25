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

            _meters = new VisualElement();
            _cards[(int)HudBlock.Status].Add(_meters);
            CreateMeter(0, "meter__fill--reputation");
            CreateMeter(1, null);
            CreateMeter(2, "meter__fill--xp");
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
