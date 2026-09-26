using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>
    /// Player settings stored in PlayerPrefs. Mono/Menu/SettingsApplier applies them to the engine;
    /// simulation code only reads the gameplay ones (autosave, game speed).
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "GasStation.Settings.";

        public static int ResolutionWidth { get; set; }
        public static int ResolutionHeight { get; set; }
        public static FullScreenMode ScreenMode { get; set; } = FullScreenMode.FullScreenWindow;
        public static bool VSync { get; set; } = true;
        /// <summary>0 = unlimited.</summary>
        public static int FrameRateLimit { get; set; }
        public static int QualityLevel { get; set; } = -1;
        public static float RenderScale { get; set; } = 1f;
        public static float MasterVolume { get; set; } = 0.8f;
        public static float EffectsVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 0.5f;
        /// <summary>0 = off, then radio stations in order.</summary>
        public static int RadioStation { get; set; } = 1;
        public static float UiScale { get; set; } = 1f;
        public static bool ShowControls { get; set; } = true;
        public static bool Autosave { get; set; } = true;
        public static int GameSpeed { get; set; } = 1;
        public static float MouseSensitivity { get; set; } = 1f;
        public static bool InvertY { get; set; }

        public static readonly int[] FrameRateOptions = { 0, 30, 60, 120, 144, 165, 240 };
        public static readonly int[] GameSpeedOptions = { 1, 2, 3 };

        public static void Load()
        {
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "Width", Screen.currentResolution.width);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "Height", Screen.currentResolution.height);
            ScreenMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "ScreenMode", (int)FullScreenMode.FullScreenWindow);
            VSync = PlayerPrefs.GetInt(Prefix + "VSync", 1) == 1;
            FrameRateLimit = PlayerPrefs.GetInt(Prefix + "FrameRate", 0);
            QualityLevel = PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel());
            RenderScale = PlayerPrefs.GetFloat(Prefix + "RenderScale", 1f);
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", 0.8f);
            EffectsVolume = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", 0.5f);
            RadioStation = PlayerPrefs.GetInt(Prefix + "RadioStation", 1);
            UiScale = PlayerPrefs.GetFloat(Prefix + "UiScale", 1f);
            ShowControls = PlayerPrefs.GetInt(Prefix + "ShowControls", 1) == 1;
            Autosave = PlayerPrefs.GetInt(Prefix + "Autosave", 1) == 1;
            GameSpeed = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "GameSpeed", 1), 1, 3);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "MouseSensitivity", 1f), 0.2f, 3f);
            InvertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "Width", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "Height", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "ScreenMode", (int)ScreenMode);
            PlayerPrefs.SetInt(Prefix + "VSync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "FrameRate", FrameRateLimit);
            PlayerPrefs.SetInt(Prefix + "Quality", QualityLevel);
            PlayerPrefs.SetFloat(Prefix + "RenderScale", RenderScale);
            PlayerPrefs.SetFloat(Prefix + "MasterVolume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "EffectsVolume", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "MusicVolume", MusicVolume);
            PlayerPrefs.SetInt(Prefix + "RadioStation", RadioStation);
            PlayerPrefs.SetFloat(Prefix + "UiScale", UiScale);
            PlayerPrefs.SetInt(Prefix + "ShowControls", ShowControls ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "Autosave", Autosave ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "GameSpeed", GameSpeed);
            PlayerPrefs.SetFloat(Prefix + "MouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "InvertY", InvertY ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Pause and game speed. The simulation stops through Time.timeScale.</summary>
    public static class GamePause
    {
        /// <summary>A menu (main, pause, settings) is open: gameplay input is ignored.</summary>
        public static bool MenuOpen { get; private set; }

        public static void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            ApplyTimeScale();
        }

        public static void SetGameSpeed(int speed)
        {
            GameSettings.GameSpeed = Mathf.Clamp(speed, 1, 3);
            ApplyTimeScale();
        }

        public static void ApplyTimeScale() => Time.timeScale = MenuOpen ? 0f : GameSettings.GameSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            MenuOpen = false;
            Time.timeScale = 1f;
        }
    }
}
