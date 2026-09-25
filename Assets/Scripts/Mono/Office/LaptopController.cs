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
    public class LaptopController : MonoBehaviour
    {
        private enum App
        {
            Mail,
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
                case App.Bank: BuildBank(content); break;
                case App.Competitor: BuildCompetitor(content); break;
                case App.Regulars: BuildRegulars(content); break;
                case App.Stats: BuildStats(content); break;
            }
        }

        // ---------------------------------------------------------------- apps

        private void BuildMail(VisualElement content)
        {
            content.Add(Label(Loc.T("laptop.mail.offers"), "laptop-heading"));
            if (HudModel.Offers.Count == 0)
                content.Add(Label(Loc.T("laptop.mail.noOffers"), "laptop-muted"));

            int level = HudModel.Level.Level;
            bool full = HudModel.Contracts.Count >= ContractMath.MaxActive;
            foreach (var offer in HudModel.Offers)
            {
                var info = ContractMath.Get(offer.Type);
                var card = Card(GameTexts.ContractName(offer.Type));
                card.Add(Label(Loc.T($"contract.{offer.Type}.desc"), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.terms", ContractMath.VehiclesPerDay(offer.Type), GameTexts.FuelName(info.Fuel),
                    offer.Price, MarketPrice(info.Fuel), offer.Days), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.stakes", info.Penalty, info.Bonus, offer.ExpiresIn), "laptop-muted"));

                var actions = Actions(card);
                int id = offer.Id;
                var accept = Action(actions, Loc.T("laptop.contract.accept"), () => StationCommands.AcceptContract(id));
                accept.SetEnabled(!full && level >= info.RequiredLevel);
                Action(actions, Loc.T("laptop.contract.decline"), () => StationCommands.DeclineContract(id));
                if (level < info.RequiredLevel)
                    card.Add(Label(Loc.F("msg.contractNeedsLevel", info.RequiredLevel), "laptop-muted"));
                else if (full)
                    card.Add(Label(Loc.F("msg.contractsFull", ContractMath.MaxActive), "laptop-muted"));
            }

            content.Add(Label(Loc.F("laptop.mail.active", HudModel.Contracts.Count, ContractMath.MaxActive), "laptop-heading"));
            if (HudModel.Contracts.Count == 0)
                content.Add(Label(Loc.T("laptop.mail.noActive"), "laptop-muted"));

            foreach (var contract in HudModel.Contracts)
            {
                var info = ContractMath.Get(contract.Type);
                var card = Card(GameTexts.ContractName(contract.Type));
                card.Add(Label(Loc.F("laptop.contract.progress", contract.DaysLeft, contract.Served, contract.Missed,
                    ContractMath.StrikesToCancel, contract.Price), "laptop-line"));
                card.Add(Label(Loc.F("laptop.contract.schedule", ScheduleText(contract.Type), GameTexts.FuelName(info.Fuel)), "laptop-muted"));

                var actions = Actions(card);
                int id = contract.Id;
                bool armed = _cancelArmedId == id && Time.unscaledTime - _cancelArmedAt < ConfirmTime * 3f;
                Action(actions, armed ? Loc.T("laptop.contract.confirmCancel") : Loc.F("laptop.contract.cancel", ContractMath.CancelPenalty(contract.Type)), () =>
                {
                    if (armed)
                    {
                        StationCommands.CancelContract(id);
                        _cancelArmedId = -1;
                    }
                    else
                    {
                        _cancelArmedId = id;
                        _cancelArmedAt = Time.unscaledTime;
                    }
                });
            }
        }

        private void BuildBank(VisualElement content)
        {
            var finance = HudModel.Finance;
            content.Add(Label(Loc.F("laptop.bank.heading", Loc.T($"difficulty.{HudModel.Difficulty}")), "laptop-heading"));

            var loanCard = Card(Loc.T("laptop.bank.loan"));
            if (finance.Loan == LoanKind.None)
            {
                foreach (var kind in new[] { LoanKind.Small, LoanKind.Large })
                {
                    loanCard.Add(Label(Loc.F("laptop.bank.offer", FinanceMath.LoanAmount(kind), FinanceMath.LoanInterest(kind) * 100f,
                        FinanceMath.LoanWeeks(kind), FinanceMath.LoanWeeklyPayment(kind)), "laptop-line"));
                }

                var actions = Actions(loanCard);
                Action(actions, Loc.F("laptop.bank.take", FinanceMath.LoanAmount(LoanKind.Small)), () => StationCommands.TakeLoan(LoanKind.Small));
                Action(actions, Loc.F("laptop.bank.take", FinanceMath.LoanAmount(LoanKind.Large)), () => StationCommands.TakeLoan(LoanKind.Large));
            }
            else
            {
                loanCard.Add(Label(Loc.F("panel.bank.loan", finance.LoanBalance, finance.WeeklyPayment), "laptop-line"));
                float early = FinanceMath.EarlyRepayment(finance.LoanBalance, finance.Loan);
                var repay = Action(Actions(loanCard), Loc.F("laptop.bank.repay", early), StationCommands.RepayLoan);
                repay.SetEnabled(HudModel.Economy.Money >= early);
            }

            var insurance = Card(Loc.T("laptop.bank.insurance"));
            float premium = FinanceMath.InsurancePremium * FinanceMath.BillMultiplier(HudModel.Difficulty);
            insurance.Add(Label(Loc.F(finance.Insured ? "laptop.bank.insured" : "laptop.bank.notInsured", premium,
                FinanceMath.InsuranceCoverage * 100f), "laptop-line"));
            Action(Actions(insurance), Loc.T(finance.Insured ? "laptop.bank.insuranceOff" : "laptop.bank.insuranceOn"), StationCommands.ToggleInsurance);

            var bills = Card(Loc.T("laptop.bank.bills"));
            bills.Add(Label(Loc.F("panel.bank.utilities", HudModel.LastUtilities), "laptop-line"));
            int daysToTax = FinanceMath.DaysPerWeek - (HudModel.Day - 1) % FinanceMath.DaysPerWeek;
            bills.Add(Label(Loc.F("panel.bank.tax", FinanceMath.TaxRate * 100f, finance.WeekRevenue,
                FinanceMath.WeeklyTax(finance.WeekRevenue), daysToTax), "laptop-line"));
            if (finance.DaysInDebt > 0)
            {
                int limit = FinanceMath.BankruptcyDays(HudModel.Difficulty);
                bills.Add(Label(limit > 0 ? Loc.F("panel.bank.debt", finance.DaysInDebt, limit) : Loc.F("panel.bank.debtRelaxed", finance.DaysInDebt),
                    "laptop-line"));
            }
        }

        private void BuildCompetitor(VisualElement content)
        {
            var rival = HudModel.Competitor;
            content.Add(Label(Loc.T("laptop.rival.heading"), "laptop-heading"));
            if (rival.BoughtOut)
            {
                content.Add(Label(Loc.F("panel.rival.boughtOut", StationProfile.DisplayName), "laptop-line"));
                return;
            }

            if (!rival.Active)
            {
                content.Add(Label(Loc.F("panel.rival.notOpen", rival.OpensOnDay), "laptop-line"));
                return;
            }

            var prices = Card(Loc.T("laptop.rival.prices"));
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = (FuelType)i;
                prices.Add(Label(Loc.F("laptop.rival.priceLine", GameTexts.FuelName(fuel), HudModel.Fuel[i].SellPrice, rival.Price(fuel),
                    HudModel.Fuel[i].MarketPrice), "laptop-line"));
            }

            var market = Card(Loc.T("laptop.rival.market"));
            market.Add(Label(Loc.F("panel.rival.share", rival.OurShare * 100f), "laptop-line"));
            market.Add(Label(Loc.F("panel.rival.reputation", HudModel.Economy.Reputation * 100f, rival.Reputation * 100f), "laptop-line"));
            if (rival.Promo != CompetitorPromo.None)
                market.Add(Label(Loc.F($"panel.rival.promo.{rival.Promo}", rival.PromoDaysLeft), "laptop-line"));

            var buyout = Card(Loc.T("laptop.rival.buyout"));
            bool canBuy = CompetitionMath.CanBuyOut(HudModel.Level.Level, HudModel.Economy.Money);
            buyout.Add(Label(HudModel.Level.Level >= CompetitionMath.BuyoutLevel
                ? Loc.F("laptop.rival.buyoutText", CompetitionMath.BuyoutPrice)
                : Loc.F("panel.rival.buyoutLocked", CompetitionMath.BuyoutPrice, CompetitionMath.BuyoutLevel), "laptop-line"));
            var button = Action(Actions(buyout), _buyoutArmed ? Loc.T("laptop.rival.confirm") : Loc.F("laptop.rival.buy", CompetitionMath.BuyoutPrice), () =>
            {
                if (_buyoutArmed)
                {
                    StationCommands.BuyOutCompetitor();
                    _buyoutArmed = false;
                }
                else
                {
                    _buyoutArmed = true;
                }
            });
            button.SetEnabled(canBuy);
        }

        private void BuildRegulars(VisualElement content)
        {
            int met = 0;
            foreach (var regular in HudModel.Regulars)
            {
                if (regular.Visits > 0)
                    met++;
            }

            content.Add(Label(Loc.F("laptop.regulars.heading", met, HudModel.Regulars.Count), "laptop-heading"));
            if (HudModel.Buzz.DaysLeft > 0)
                content.Add(Label(Loc.F(HudModel.Buzz.Factor > 1f ? "panel.regulars.praise" : "panel.regulars.pan", HudModel.Buzz.DaysLeft), "laptop-line"));
            if (met == 0)
                content.Add(Label(Loc.T("panel.regulars.none"), "laptop-muted"));

            for (int i = 0; i < HudModel.Regulars.Count && i < RegularCatalog.Count; i++)
            {
                var regular = HudModel.Regulars[i];
                if (regular.Visits == 0)
                    continue;

                var info = RegularCatalog.Get(i);
                var card = Card(GameTexts.RegularName(i + 1));
                card.Add(Label(GameTexts.RegularAbout(i + 1), "laptop-muted"));
                int hearts = Mathf.Clamp(Mathf.RoundToInt(regular.Loyalty * 5f), 0, 5);
                string status = regular.Lost ? Loc.T("panel.regulars.lost")
                    : new string('♥', hearts) + new string('♡', 5 - hearts);
                card.Add(Label(Loc.F("laptop.regulars.line", GameTexts.FuelName(info.Fuel), info.Hour, regular.Visits, status), "laptop-line"));
            }
        }

        private void BuildStats(VisualElement content)
        {
            var economy = HudModel.Economy;
            content.Add(Label(Loc.T("laptop.stats.heading"), "laptop-heading"));

            var money = Card(Loc.T("laptop.stats.money"));
            money.Add(Label(Loc.F("panel.finance.today", economy.DayIncome, economy.DayExpenses, economy.DayIncome - economy.DayExpenses), "laptop-line"));
            float weekIncome = 0f, weekExpenses = 0f;
            int from = Math.Max(0, HudModel.History.Count - 7);
            for (int i = from; i < HudModel.History.Count; i++)
            {
                weekIncome += HudModel.History[i].Income;
                weekExpenses += HudModel.History[i].Expenses;
            }

            if (HudModel.History.Count > 0)
            {
                var yesterday = HudModel.History[HudModel.History.Count - 1];
                money.Add(Label(Loc.F("panel.finance.yesterday", yesterday.Income, yesterday.Expenses, yesterday.Income - yesterday.Expenses), "laptop-line"));
                money.Add(Label(Loc.F("panel.finance.week", HudModel.History.Count - from, weekIncome, weekExpenses, weekIncome - weekExpenses), "laptop-line"));
            }

            var stats = HudModel.Stats;
            var station = Card(Loc.T("laptop.stats.station"));
            station.Add(Label(Loc.F("panel.achievements.stats", stats.DaysPlayed, stats.Served, stats.Income), "laptop-line"));
            float rating = stats.RatingCount > 0 ? stats.RatingSum / stats.RatingCount : 0f;
            station.Add(Label(stats.RatingCount > 0
                ? Loc.F("panel.finance.rating", StarText(Mathf.RoundToInt(rating)), rating, stats.RatingCount)
                : Loc.T("panel.finance.noRating"), "laptop-line"));

            var reviews = Card(Loc.T("panel.finance.reviews"));
            if (HudModel.Reviews.Count == 0)
                reviews.Add(Label(Loc.T("laptop.stats.noReviews"), "laptop-muted"));
            foreach (var review in HudModel.Reviews)
            {
                string author = review.RegularId > 0 ? GameTexts.RegularName(review.RegularId) + ": " : string.Empty;
                reviews.Add(Label($"{StarText(review.Stars)}  {author}{Loc.T($"review.{review.Stars}.{review.Variant}")}", "laptop-line"));
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string StarText(int stars) =>
            new string('★', Mathf.Clamp(stars, 0, 5)) + new string('☆', 5 - Mathf.Clamp(stars, 0, 5));

        private static float MarketPrice(FuelType fuel) => HudModel.Fuel[(int)fuel].MarketPrice;

        private static string ScheduleText(ContractType type)
        {
            var info = ContractMath.Get(type);
            if (info.EveryHours <= 0)
                return Loc.F("laptop.contract.daily", info.FirstHour, info.VehiclesPerSlot);
            return Loc.F("laptop.contract.every", info.EveryHours, info.FirstHour, info.LastHour, info.VehiclesPerSlot);
        }

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
