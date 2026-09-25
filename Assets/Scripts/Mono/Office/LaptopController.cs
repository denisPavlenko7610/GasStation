using System;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Mono.Hud;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GasStation.Mono.Office
{
    /// <summary>
    /// The office laptop: walk up to it and press E (or N anywhere). A desktop with apps you click with the
    /// mouse: mail with contract offers, bank, the competitor, regulars and statistics. The game is paused
    /// while it is open; the hotkey panels keep working as before.
    /// </summary>
    public partial class LaptopController : MonoBehaviour
    {
        private enum App
        {
            Mail,
            Staff,
            Suppliers,
            Events,
            Skills,
            Collection,
            Bank,
            Competitor,
            Regulars,
            Stats
        }

        private const float ConfirmTime = 2f;

        private VisualElement _root;
        private App _app = App.Mail;
        private int _cancelArmedId = -1;
        private float _cancelArmedAt = float.NegativeInfinity;
        private bool _buyoutArmed;
        private int _fireArmedId = -1;

        private void Start()
        {
            var menuStyle = Resources.Load<StyleSheet>("UI/Menu");
            var laptopStyle = Resources.Load<StyleSheet>("UI/Laptop");
            var document = menuStyle != null && laptopStyle != null ? UiToolkit.CreateDocument(gameObject, "Laptop (UI Toolkit)", 150) : null;
            if (document == null)
            {
                Debug.LogWarning("GasStation: laptop styles are missing in Resources/UI; the laptop is disabled.");
                enabled = false;
                return;
            }

            _root = document.rootVisualElement;
            _root.styleSheets.Add(menuStyle);
            _root.styleSheets.Add(laptopStyle);
            _root.pickingMode = PickingMode.Ignore;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _root == null)
                return;

            if (LaptopState.IsOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.nKey.wasPressedThisFrame || HudModel.GameOver)
                    Close();
                return;
            }

            if (GamePause.MenuOpen || BuildMode.Active || !HudModel.HasStation)
                return;

            bool atDesk = keyboard.eKey.wasPressedThisFrame && HudModel.Hint == InteractionHint.Laptop;
            if (atDesk || keyboard.nKey.wasPressedThisFrame)
                Open();
        }

        private void Open()
        {
            LaptopState.SetOpen(true);
            _root.pickingMode = PickingMode.Position;
            Rebuild();
        }

        private void Close()
        {
            LaptopState.SetOpen(false);
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;
        }

        /// <summary>Commands are applied by the simulation on its next update; redraw a moment later.</summary>
        private void RefreshSoon() => _root.schedule.Execute(() =>
        {
            if (LaptopState.IsOpen)
                Rebuild();
        }).StartingIn(120);

        // ---------------------------------------------------------------- layout

        private void Rebuild()
        {
            _root.Clear();
            var overlay = new VisualElement();
            overlay.AddToClassList("menu-overlay");
            _root.Add(overlay);

            var window = new VisualElement();
            window.AddToClassList("menu-window");
            window.AddToClassList("laptop-window");
            overlay.Add(window);

            var titlebar = new VisualElement();
            titlebar.AddToClassList("laptop-titlebar");
            titlebar.Add(Label(Loc.F("laptop.title", StationProfile.DisplayName, HudModel.Day, HudModel.Economy.Money), "laptop-title"));
            var close = new Button(Close) { text = "✕" };
            close.AddToClassList("laptop-close");
            titlebar.Add(close);
            window.Add(titlebar);

            var body = new VisualElement();
            body.AddToClassList("laptop-body");
            window.Add(body);

            var apps = new VisualElement();
            apps.AddToClassList("laptop-apps");
            body.Add(apps);
            foreach (App app in Enum.GetValues(typeof(App)))
            {
                string text = Loc.T($"laptop.app.{app}");
                if (app == App.Mail && HudModel.Offers.Count > 0)
                    text += $" ({HudModel.Offers.Count})";
                var button = new Button(() =>
                {
                    _app = app;
                    Rebuild();
                }) { text = text };
                button.AddToClassList("laptop-app");
                if (app == _app)
                    button.AddToClassList("laptop-app--active");
                apps.Add(button);
            }

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("laptop-content");
            body.Add(content);
            _currentContent = content;

            switch (_app)
            {
                case App.Mail: BuildMail(content); break;
                case App.Staff: BuildStaff(content); break;
                case App.Suppliers: BuildSuppliers(content); break;
                case App.Events: BuildEvents(content); break;
                case App.Skills: BuildSkills(content); break;
                case App.Collection: BuildCollection(content); break;
                case App.Bank: BuildBank(content); break;
                case App.Competitor: BuildCompetitor(content); break;
                case App.Regulars: BuildRegulars(content); break;
                case App.Stats: BuildStats(content); break;
            }
        }

        // ---------------------------------------------------------------- helpers (apps: LaptopController.Business.cs, .Station.cs)

        private static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        /// <summary>A titled card, added to the content of the app being built.</summary>
        private VisualElement Card(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("laptop-card");
            card.Add(Label(title, "laptop-card-title"));
            _currentContent?.Add(card);
            return card;
        }

        private static VisualElement Actions(VisualElement card)
        {
            var actions = new VisualElement();
            actions.AddToClassList("laptop-actions");
            card.Add(actions);
            return actions;
        }

        private Button Action(VisualElement actions, string text, System.Action onClick)
        {
            var button = new Button(() =>
            {
                onClick();
                RefreshSoon();
            }) { text = text };
            button.AddToClassList("laptop-action");
            actions.Add(button);
            return button;
        }

        private VisualElement _currentContent;
    }
}
