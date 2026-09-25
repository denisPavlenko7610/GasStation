using System.Collections.Generic;
using System.Linq;
using GasStation.Bridge;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GasStation.Mono.Menu
{
    /// <summary>Applies GameSettings to the screen, graphics and audio.</summary>
    public static class SettingsApplier
    {
        /// <summary>Distinct resolutions of the display, largest first.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var result = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderByDescending(r => r.x * r.y)
                .ToList();

            if (result.Count == 0)
                result.Add(new Vector2Int(Screen.width, Screen.height));
            return result;
        }

        public static void ApplyAll()
        {
            ApplyDisplay();
            ApplyGraphics();
            ApplyAudio();
            GamePause.ApplyTimeScale();
        }

        public static void ApplyDisplay()
        {
            if (Application.isEditor)
                return;

            int width = GameSettings.ResolutionWidth > 0 ? GameSettings.ResolutionWidth : Screen.currentResolution.width;
            int height = GameSettings.ResolutionHeight > 0 ? GameSettings.ResolutionHeight : Screen.currentResolution.height;
            Screen.SetResolution(width, height, GameSettings.ScreenMode);
        }

        public static void ApplyGraphics()
        {
            if (GameSettings.QualityLevel >= 0 && GameSettings.QualityLevel < QualitySettings.names.Length &&
                GameSettings.QualityLevel != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(GameSettings.QualityLevel, true);
            }

            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
            Application.targetFrameRate = GameSettings.VSync || GameSettings.FrameRateLimit <= 0 ? -1 : GameSettings.FrameRateLimit;

            // In the editor this would modify the pipeline asset on disk.
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(GameSettings.RenderScale, 0.5f, 1f);
        }

        public static void ApplyAudio() => AudioListener.volume = Mathf.Clamp01(GameSettings.MasterVolume);
    }
}
