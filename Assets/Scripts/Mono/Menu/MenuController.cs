using System;
using System.Collections.Generic;
using System.Linq;
using GasStation.Bridge;
using GasStation.Localization;
using GasStation.Mono.Hud;
using GasStation.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GasStation.Mono.Menu
{
    /// <summary>
    /// Main menu, pause menu (Esc), settings and controls, drawn with UI Toolkit. While a menu is open the
    /// simulation is paused (GamePause) and gameplay keys are ignored.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        private const string StylePath = "UI/Menu";

        private enum MenuScreen
        {
            None,
            Main,
            Pause,
            Settings,
            Controls,
            Confirm,
            Welcome,
            StationName,
            Difficulty,
            GameOver
        }

        private const string TutorialSeenKey = "GasStation.TutorialSeen";
        private const int TutorialPages = 4;
        private int _tutorialPage;

        private VisualElement _root;
        private MenuScreen _screen = MenuScreen.None;
        private MenuScreen _returnTo = MenuScreen.Main;
        private Action _confirmAction;
        private string _confirmText;

        private void Start()
        {
            var style = Resources.Load<StyleSheet>(StylePath);
            var document = style != null ? UiToolkit.CreateDocument(gameObject, "Menu (UI Toolkit)", 200) : null;
            if (document == null)
            {
                Debug.LogWarning("GasStation: menu assets are missing in Resources/UI; the game starts without a menu.");
                enabled = false;
                return;
            }

            _root = document.rootVisualElement;
            _root.styleSheets.Add(style);
            _root.pickingMode = PickingMode.Ignore;
            Show(MenuScreen.Main);
        }

        private void Update()
        {
            if (HudModel.GameOver && _screen is not (MenuScreen.GameOver or MenuScreen.Difficulty or MenuScreen.Confirm
                    or MenuScreen.Settings or MenuScreen.Controls))
            {
                Show(MenuScreen.GameOver);
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            switch (_screen)
            {
                case MenuScreen.None:
                    Show(MenuScreen.Pause);
                    break;
                case MenuScreen.Pause:
                    Show(MenuScreen.None);
                    break;
                case MenuScreen.Settings:
                    GameSettings.Save();
                    Show(_returnTo);
                    break;
                case MenuScreen.Controls:
                case MenuScreen.Confirm:
                case MenuScreen.StationName:
                case MenuScreen.Difficulty:
                    Show(_returnTo);
                    break;
                case MenuScreen.Welcome:
                    FinishTutorial();
                    break;
            }
        }

        private void Show(MenuScreen screen)
        {
            if (screen is MenuScreen.Main or MenuScreen.Pause or MenuScreen.GameOver)
                _returnTo = screen;

            _screen = screen;
            _root.Clear();
            GamePause.SetMenuOpen(screen != MenuScreen.None);

            switch (screen)
            {
                case MenuScreen.Main: BuildMain(); break;
                case MenuScreen.Pause: BuildPause(); break;
                case MenuScreen.Settings: BuildSettings(); break;
                case MenuScreen.Controls: BuildControls(); break;
                case MenuScreen.Confirm: BuildConfirm(); break;
                case MenuScreen.Welcome: BuildWelcome(); break;
                case MenuScreen.StationName: BuildStationName(); break;
                case MenuScreen.Difficulty: BuildDifficulty(); break;
                case MenuScreen.GameOver: BuildGameOver(); break;
            }
        }

        // ---------------------------------------------------------------- screens

        private void BuildMain()
        {
            var window = Window(main: true);
            window.Add(Text(Loc.T("menu.title"), "menu-title"));
            window.Add(Text(Loc.T("menu.subtitle"), "menu-subtitle"));

            bool hasSave = SaveService.Exists;
            window.Add(MenuButton(hasSave ? Loc.T("menu.continue") : Loc.T("menu.play"), StartPlaying));
            window.Add(MenuButton(Loc.T("menu.newGame"), () =>
            {
                if (!hasSave)
                {
                    Show(MenuScreen.Difficulty);
                    return;
                }

                AskConfirm(Loc.T("menu.confirm.newGame"), () => Show(MenuScreen.Difficulty));
            }));
            window.Add(MenuButton(Loc.T("menu.settings"), () => Show(MenuScreen.Settings)));
            window.Add(MenuButton(Loc.T("menu.controls"), () => Show(MenuScreen.Controls)));
            window.Add(MenuButton(Loc.T("menu.quit"), Quit));
            window.Add(Text(Loc.F("menu.version", Application.version), "menu-footer"));
        }

        private void BuildPause()
        {
            var window = Window();
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.pause"), "menu-heading"));
            window.Add(MenuButton(Loc.T("menu.resume"), () => Show(MenuScreen.None)));
            window.Add(MenuButton(Loc.T("menu.save"), () =>
            {
                StationCommands.SaveGame();
                Show(MenuScreen.None);
            }));
            var load = MenuButton(Loc.T("menu.load"), () =>
                AskConfirm(Loc.T("menu.confirm.load"), () =>
                {
                    StationCommands.LoadGame();
                    Show(MenuScreen.None);
                }));
            load.SetEnabled(SaveService.Exists);
            window.Add(load);
            window.Add(MenuButton(Loc.T("menu.stationName"), () => Show(MenuScreen.StationName)));
            window.Add(MenuButton(Loc.T("menu.settings"), () => Show(MenuScreen.Settings)));
            window.Add(MenuButton(Loc.T("menu.controls"), () => Show(MenuScreen.Controls)));
            window.Add(MenuButton(Loc.T("menu.toMainMenu"), () =>
            {
                StationCommands.SaveGame();
                Show(MenuScreen.Main);
            }));
            window.Add(MenuButton(Loc.T("menu.quit"), Quit));
        }

        private void BuildSettings()
        {
            var window = Window(wide: true);
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.settings"), "menu-heading"));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("settings-scroll");
            window.Add(scroll);

            // Display
            scroll.Add(Text(Loc.T("settings.display"), "menu-section"));
            var resolutions = SettingsApplier.Resolutions();
            int currentResolution = Mathf.Max(0, resolutions.FindIndex(r =>
                r.x == GameSettings.ResolutionWidth && r.y == GameSettings.ResolutionHeight));
            scroll.Add(Dropdown(Loc.T("settings.resolution"), resolutions.Select(r => $"{r.x} × {r.y}").ToList(), currentResolution, index =>
            {
                GameSettings.ResolutionWidth = resolutions[index].x;
                GameSettings.ResolutionHeight = resolutions[index].y;
                SettingsApplier.ApplyDisplay();
            }));

            var modes = new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            scroll.Add(Dropdown(Loc.T("settings.mode"),
                modes.Select(m => Loc.T($"settings.mode.{m}")).ToList(),
                Mathf.Max(0, Array.IndexOf(modes, GameSettings.ScreenMode)), index =>
                {
                    GameSettings.ScreenMode = modes[index];
                    SettingsApplier.ApplyDisplay();
                }));

            scroll.Add(ToggleRow(Loc.T("settings.vsync"), GameSettings.VSync, value =>
            {
                GameSettings.VSync = value;
                SettingsApplier.ApplyGraphics();
            }));

            var frameRates = GameSettings.FrameRateOptions;
            scroll.Add(Dropdown(Loc.T("settings.fps"),
                frameRates.Select(f => f == 0 ? Loc.T("settings.fps.unlimited") : f.ToString()).ToList(),
                Mathf.Max(0, Array.IndexOf(frameRates, GameSettings.FrameRateLimit)), index =>
                {
                    GameSettings.FrameRateLimit = frameRates[index];
                    SettingsApplier.ApplyGraphics();
                }));

            // Graphics
            scroll.Add(Text(Loc.T("settings.graphics"), "menu-section"));
            scroll.Add(Dropdown(Loc.T("settings.quality"), QualitySettings.names.ToList(),
                Mathf.Clamp(GameSettings.QualityLevel, 0, QualitySettings.names.Length - 1), index =>
                {
                    GameSettings.QualityLevel = index;
                    SettingsApplier.ApplyGraphics();
                }));
            scroll.Add(SliderRow(Loc.T("settings.renderScale"), 0.5f, 1f, GameSettings.RenderScale, value =>
            {
                GameSettings.RenderScale = value;
                SettingsApplier.ApplyGraphics();
            }));

            // Audio
            scroll.Add(Text(Loc.T("settings.audio"), "menu-section"));
            scroll.Add(SliderRow(Loc.T("settings.masterVolume"), 0f, 1f, GameSettings.MasterVolume, value =>
            {
                GameSettings.MasterVolume = value;
                SettingsApplier.ApplyAudio();
            }));
            scroll.Add(SliderRow(Loc.T("settings.effectsVolume"), 0f, 1f, GameSettings.EffectsVolume,
                value => GameSettings.EffectsVolume = value));
            scroll.Add(SliderRow(Loc.T("settings.musicVolume"), 0f, 1f, GameSettings.MusicVolume,
                value => GameSettings.MusicVolume = value));
            var stations = new[] { Audio.RadioStation.Off, Audio.RadioStation.Country, Audio.RadioStation.Synthwave, Audio.RadioStation.LoFi };
            scroll.Add(Dropdown(Loc.T("settings.radio"), stations.Select(Audio.RadioPlayer.StationName).ToList(),
                Mathf.Clamp(GameSettings.RadioStation, 0, stations.Length - 1), index => GameSettings.RadioStation = index));

            // Interface
            scroll.Add(Text(Loc.T("settings.interface"), "menu-section"));
            scroll.Add(Dropdown(Loc.T("settings.language"),
                new List<string> { "Русский", "English" }, (int)Loc.Language, index =>
                {
                    Loc.SetLanguage((GameLanguage)index);
                    // Rebuild on the next frame so the dropdown finishes its own event first.
                    _root.schedule.Execute(() => Show(MenuScreen.Settings));
                }));
            scroll.Add(SliderRow(Loc.T("settings.uiScale"), 0.75f, 1.5f, GameSettings.UiScale, value =>
            {
                GameSettings.UiScale = value;
                UiToolkit.ApplyScale(value);
            }));
            scroll.Add(ToggleRow(Loc.T("settings.showControls"), GameSettings.ShowControls,
                value => GameSettings.ShowControls = value));

            // Game
            scroll.Add(Text(Loc.T("settings.game"), "menu-section"));
            var speeds = GameSettings.GameSpeedOptions;
            scroll.Add(Dropdown(Loc.T("settings.gameSpeed"), speeds.Select(s => $"×{s}").ToList(),
                Mathf.Max(0, Array.IndexOf(speeds, GameSettings.GameSpeed)), index => GameSettings.GameSpeed = speeds[index]));
            scroll.Add(ToggleRow(Loc.T("settings.autosave"), GameSettings.Autosave, value => GameSettings.Autosave = value));

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.back"), () =>
            {
                GameSettings.Save();
                Show(_returnTo);
            }));
            window.Add(buttons);
        }

        private void BuildControls()
        {
            var window = Window(wide: true);
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.controls"), "menu-heading"));
            window.Add(Text(Loc.T("controls.text"), "menu-text"));

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.tutorial"), () =>
            {
                _tutorialPage = 0;
                Show(MenuScreen.Welcome);
            }));
            buttons.Add(SmallButton(Loc.T("menu.back"), () => Show(_returnTo)));
            window.Add(buttons);
        }

        private void BuildConfirm()
        {
            var window = Window();
            window.Add(Accent());
            window.Add(Text(_confirmText, "menu-text"));

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.no"), () => Show(_returnTo)));
            buttons.Add(SmallButton(Loc.T("menu.yes"), () => _confirmAction?.Invoke()));
            window.Add(buttons);
        }

        // ---------------------------------------------------------------- actions

        private void AskConfirm(string text, Action action)
        {
            _confirmText = text;
            _confirmAction = action;
            Show(MenuScreen.Confirm);
        }

        private void StartNewGame(Components.Difficulty difficulty)
        {
            StationCommands.NewGame(difficulty);
            // The simulation clears the flag on its next update; until then the menu must not reopen it.
            HudModel.GameOver = false;
            StartPlaying();
        }

        /// <summary>Closes the menu; the first time ever, shows the short introduction first.</summary>
        private void StartPlaying()
        {
            if (PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1)
            {
                Show(MenuScreen.None);
                return;
            }

            _tutorialPage = 0;
            Show(MenuScreen.Welcome);
        }

        private void BuildDifficulty()
        {
            var window = Window(wide: true);
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.difficulty"), "menu-heading"));

            foreach (var difficulty in new[] { Components.Difficulty.Relaxed, Components.Difficulty.Normal, Components.Difficulty.Survival })
            {
                window.Add(MenuButton(Loc.T($"difficulty.{difficulty}"), () => StartNewGame(difficulty)));
                window.Add(Text(Loc.T($"difficulty.{difficulty}.text"), "menu-text"));
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.back"), () => Show(_returnTo)));
            window.Add(buttons);
        }

        private void BuildGameOver()
        {
            var window = Window();
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.gameOver"), "menu-heading"));
            window.Add(Text(Loc.F("menu.gameOver.text", StationProfile.DisplayName, HudModel.Day,
                HudModel.Stats.Served, HudModel.Level.Level), "menu-text"));
            window.Add(MenuButton(Loc.T("menu.newGame"), () => Show(MenuScreen.Difficulty)));
            window.Add(MenuButton(Loc.T("menu.quit"), Quit));
        }

        private void BuildStationName()
        {
            var window = Window();
            window.Add(Accent());
            window.Add(Text(Loc.T("menu.stationName"), "menu-heading"));
            window.Add(Text(Loc.T("menu.stationName.hint"), "menu-text"));

            var field = new TextField { maxLength = StationProfile.MaxNameLength, value = StationProfile.CustomName ?? string.Empty };
            field.AddToClassList("name-field");
            window.Add(field);

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.back"), () => Show(_returnTo)));
            buttons.Add(SmallButton(Loc.T("menu.saveName"), () =>
            {
                StationProfile.SetName(field.value);
                Show(_returnTo);
            }));
            window.Add(buttons);
            field.schedule.Execute(() => field.Focus());
        }

        private void BuildWelcome()
        {
            var window = Window(wide: true);
            window.Add(Accent());
            window.Add(Text(Loc.T($"tutorial.{_tutorialPage}.title"), "menu-heading"));
            window.Add(Text(Loc.T($"tutorial.{_tutorialPage}.text"), "menu-text"));
            window.Add(Text(Loc.F("tutorial.page", _tutorialPage + 1, TutorialPages), "menu-footer"));

            var buttons = new VisualElement();
            buttons.AddToClassList("menu-buttons-row");
            buttons.Add(SmallButton(Loc.T("menu.skip"), FinishTutorial));
            bool last = _tutorialPage >= TutorialPages - 1;
            buttons.Add(SmallButton(last ? Loc.T("menu.start") : Loc.T("menu.next"), () =>
            {
                if (last)
                {
                    FinishTutorial();
                    return;
                }

                _tutorialPage++;
                Show(MenuScreen.Welcome);
            }));
            window.Add(buttons);
        }

        private void FinishTutorial()
        {
            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            PlayerPrefs.Save();
            Show(MenuScreen.None);
        }

        private static void Quit()
        {
            StationCommands.SaveGame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- widgets

        private VisualElement Window(bool main = false, bool wide = false)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("menu-overlay");
            if (main)
                overlay.AddToClassList("menu-overlay--main");
            _root.Add(overlay);

            var window = new VisualElement();
            window.AddToClassList("menu-window");
            if (wide)
                window.AddToClassList("menu-window--wide");
            overlay.Add(window);
            return window;
        }

        private static VisualElement Accent()
        {
            var accent = new VisualElement();
            accent.AddToClassList("menu-accent");
            return accent;
        }

        private static Label Text(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        private static Button MenuButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("menu-button");
            return button;
        }

        private static Button SmallButton(string text, Action onClick)
        {
            var button = MenuButton(text, onClick);
            button.AddToClassList("menu-button--small");
            return button;
        }

        private static VisualElement Row(string label, VisualElement control)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");
            row.Add(Text(label, "settings-label"));
            control.AddToClassList("settings-control");
            row.Add(control);
            return row;
        }

        private static VisualElement Dropdown(string label, List<string> choices, int index, Action<int> onChange)
        {
            var dropdown = new DropdownField(choices, Mathf.Clamp(index, 0, Mathf.Max(0, choices.Count - 1)));
            dropdown.RegisterValueChangedCallback(_ => onChange(dropdown.index));
            return Row(label, dropdown);
        }

        private static VisualElement ToggleRow(string label, bool value, Action<bool> onChange)
        {
            var toggle = new Toggle { value = value };
            toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return Row(label, toggle);
        }

        private static VisualElement SliderRow(string label, float min, float max, float value, Action<float> onChange)
        {
            var slider = new Slider(min, max) { value = Mathf.Clamp(value, min, max), showInputField = false };
            slider.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return Row(label, slider);
        }
    }
}
