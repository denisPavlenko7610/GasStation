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

        public void SetMeters(HudMeters meters)
        {
            // The status text already shows these values as numbers.
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
