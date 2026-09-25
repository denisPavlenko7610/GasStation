using System.Collections.Generic;
using GasStation.Bridge;
using UnityEngine;
using UnityEngine.UIElements;

namespace GasStation.Mono.Hud
{
    /// <summary>Creates UI Toolkit documents with the game's theme (Resources/UI) and keeps their scale in sync.</summary>
    public static class UiToolkit
    {
        private const string ThemePath = "UI/GasStationTheme";

        private static readonly List<PanelSettings> Panels = new();

        public static bool Available => Resources.Load<ThemeStyleSheet>(ThemePath) != null;

        /// <summary>A document under host drawn above lower sorting orders. Null when the theme is missing.</summary>
        public static UIDocument CreateDocument(GameObject host, string name, int sortingOrder)
        {
            var theme = Resources.Load<ThemeStyleSheet>(ThemePath);
            if (theme == null)
                return null;

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = name;
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.match = 0.5f;
            panelSettings.scale = GameSettings.UiScale;
            panelSettings.sortingOrder = sortingOrder;
            Panels.Add(panelSettings);

            // Assign the panel before the document is enabled.
            var documentObject = new GameObject(name);
            documentObject.SetActive(false);
            documentObject.transform.SetParent(host.transform, false);
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            documentObject.SetActive(true);

            // The built-in dynamic font falls back to system fonts, so Cyrillic always renders.
            document.rootVisualElement.style.unityFontDefinition =
                FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            return document;
        }

        public static void ApplyScale(float scale)
        {
            Panels.RemoveAll(panel => panel == null);
            foreach (var panel in Panels)
                panel.scale = scale;
        }
    }
}
