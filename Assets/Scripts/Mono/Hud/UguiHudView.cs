using UnityEngine;
using UnityEngine.UI;

namespace GasStation.Mono.Hud
{
    /// <summary>Fallback HUD: text blocks on semi-transparent uGUI panels that size to their content.</summary>
    public class UguiHudView : IHudView
    {
        private static readonly Color PanelColor = new(0.05f, 0.06f, 0.08f, 0.62f);

        private readonly Canvas _canvas;
        private readonly Text[] _blocks = new Text[6];
        private readonly Text _marker;
        private readonly System.Collections.Generic.List<Text> _moneyPool = new();

        public UguiHudView(GameObject host)
        {
            _canvas = host.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _blocks[(int)HudBlock.Status] = CreateBlock(host.transform, "Status", font, new Vector2(0f, 1f), TextAnchor.UpperLeft, 26);
            _blocks[(int)HudBlock.Fuel] = CreateBlock(host.transform, "Fuel", font, new Vector2(1f, 1f), TextAnchor.UpperRight, 24);
            _blocks[(int)HudBlock.Pumps] = CreateBlock(host.transform, "Pumps", font, new Vector2(0f, 0f), TextAnchor.LowerLeft, 22);
            _blocks[(int)HudBlock.Center] = CreateBlock(host.transform, "Center", font, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, 32);
            _blocks[(int)HudBlock.Help] = CreateBlock(host.transform, "Help", font, new Vector2(1f, 0f), TextAnchor.LowerRight, 18);
            _blocks[(int)HudBlock.Panel] = CreateBlock(host.transform, "Panel", font, new Vector2(0.5f, 1f), TextAnchor.UpperLeft, 24);

            var markerObject = new GameObject("QuestMarker", typeof(RectTransform));
            markerObject.transform.SetParent(host.transform, false);
            _marker = markerObject.AddComponent<Text>();
            _marker.font = font;
            _marker.fontSize = 48;
            _marker.alignment = TextAnchor.LowerCenter;
            _marker.color = new Color(1f, 0.6f, 0.15f);
            _marker.text = "▼";
            _marker.raycastTarget = false;
            ((RectTransform)markerObject.transform).sizeDelta = new Vector2(80f, 80f);
            markerObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        }

        public void SetVisible(bool visible) => _canvas.enabled = visible;

        public void SetText(HudBlock block, string value)
        {
            var text = _blocks[(int)block];
            bool visible = !string.IsNullOrEmpty(value);
            var panel = text.transform.parent.gameObject;
            if (panel.activeSelf != visible)
                panel.SetActive(visible);
            if (visible && text.text != value)
                text.text = value;
        }

        public void SetCrosshair(bool visible)
        {
        }

        public void SetMeters(HudMeters meters)
        {
            // The status text already shows these values as numbers.
        }

        public void SetCards(System.Collections.Generic.IReadOnlyList<GasStation.Bridge.CarCard> cards,
            System.Collections.Generic.IReadOnlyList<string> texts)
        {
            // The fallback HUD lists the pumps as text instead.
        }

        public void SetChart(System.Collections.Generic.IReadOnlyList<GasStation.Components.DayHistoryEntry> history)
        {
            // The fallback HUD shows the finance table as text only.
        }

        public void SetMoneyPopups(System.Collections.Generic.IReadOnlyList<GasStation.Bridge.MoneyPopup> popups)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (_moneyPool.Count < popups.Count)
            {
                var go = new GameObject("MoneyPopup", typeof(RectTransform));
                go.transform.SetParent(_canvas.transform, false);
                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = 30;
                text.fontStyle = FontStyle.Bold;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                _moneyPool.Add(text);
            }

            for (int i = 0; i < _moneyPool.Count; i++)
            {
                var text = _moneyPool[i];
                if (i >= popups.Count)
                {
                    text.gameObject.SetActive(false);
                    continue;
                }

                var popup = popups[i];
                float age = Time.time - popup.BornAt;
                text.gameObject.SetActive(true);
                text.text = popup.Text;
                var tint = popup.Income ? new Color(0.55f, 0.95f, 0.6f) : new Color(1f, 0.55f, 0.5f);
                tint.a = Mathf.Clamp01(1f - age / 1.4f);
                text.color = tint;

                var rect = (RectTransform)text.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(24f, -64f + age * 46f);
                rect.sizeDelta = new Vector2(240f, 36f);
            }
        }

        public void SetMarker(bool visible, Vector3 worldPosition)
        {
            var camera = Camera.main;
            Vector3 screen = camera != null ? camera.WorldToScreenPoint(worldPosition + Vector3.up * 3f) : Vector3.back;
            bool onScreen = visible && screen.z > 0f;
            _marker.enabled = onScreen;
            if (onScreen)
                _marker.transform.position = screen + Vector3.up * (Mathf.Sin(Time.unscaledTime * 4f) * 8f);
        }

        private static Text CreateBlock(Transform parent, string name, Font font, Vector2 anchor, TextAnchor alignment, int fontSize)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

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
