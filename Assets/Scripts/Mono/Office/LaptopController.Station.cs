using System;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GasStation.Mono.Office
{
    /// <summary>Laptop apps about the station itself: events, skills, collection, regulars, statistics.</summary>
    public partial class LaptopController
    {
        private void BuildEvents(VisualElement content)
        {
            content.Add(Label(Loc.F("laptop.events.stars", GameTexts.Stars(HudModel.Stars.Stars)), "laptop-heading"));
            int next = HudModel.Stars.Stars + 1;
            if (next <= StarMath.MaxStars)
            {
                var need = StarMath.Requirement(next);
                var card = Card(Loc.F("laptop.events.next", GameTexts.Stars(next)));
                card.Add(Label(Loc.F("laptop.events.need", need.Rating, need.Cleanliness * 100f, need.Level, need.Services), "laptop-line"));
                card.Add(Label(Loc.F("laptop.events.starsHint", StarMath.EvLicenceStars), "laptop-muted"));
            }

            var road = Card(Loc.T("laptop.events.road"));
            road.Add(Label(HudModel.Road.Kind == RoadEventKind.None
                ? Loc.T("laptop.events.roadCalm")
                : Loc.F("hud.road", Loc.T($"road.{HudModel.Road.Kind}"), HudModel.Road.HoursLeft), "laptop-line"));
            if (HudModel.Road.Kind != RoadEventKind.None)
                road.Add(Label(Loc.T($"road.{HudModel.Road.Kind}.desc"), "laptop-muted"));

            content.Add(Label(Loc.T("laptop.events.host"), "laptop-heading"));
            var hosted = HudModel.Hosted;
            if (hosted.Active != HostedEventKind.None)
                content.Add(Label(Loc.F("hud.hosted", Loc.T($"hosted.{hosted.Active}"), hosted.Attendees), "laptop-line"));
            else if (hosted.Planned != HostedEventKind.None)
                content.Add(Label(Loc.F("laptop.events.planned", Loc.T($"hosted.{hosted.Planned}"), hosted.PlannedDay,
                    HostedEventMath.Get(hosted.Planned).StartHour), "laptop-line"));

            bool free = hosted.Planned == HostedEventKind.None && hosted.Active == HostedEventKind.None &&
                        HostedEventMath.CanPlan(HudModel.Day + 1, hosted.LastHostedDay);
            if (!free && hosted.Planned == HostedEventKind.None && hosted.Active == HostedEventKind.None)
                content.Add(Label(Loc.F("msg.eventTooSoon", hosted.LastHostedDay + HostedEventMath.EveryDays), "laptop-muted"));

            foreach (var kind in new[] { HostedEventKind.Fair, HostedEventKind.CarMeet, HostedEventKind.MovieNight })
            {
                var info = HostedEventMath.Get(kind);
                var card = Card(Loc.T($"hosted.{kind}"));
                card.Add(Label(Loc.T($"hosted.{kind}.desc"), "laptop-muted"));
                card.Add(Label(Loc.F("laptop.events.terms", info.StartHour, info.EndHour, info.Cost, info.Ticket, (info.Crowd - 1f) * 100f,
                    info.RequiredLevel), "laptop-line"));
                var planKind = kind;
                var plan = Action(Actions(card), Loc.F("laptop.events.plan", info.Cost), () => StationCommands.PlanEvent(planKind));
                plan.SetEnabled(free && HudModel.Level.Level >= info.RequiredLevel && HudModel.Economy.Money >= info.Cost);
            }

            content.Add(Label(Loc.T("laptop.events.prepHint"), "laptop-muted"));
        }

        private void BuildCollection(VisualElement content)
        {
            int seen = HudModel.Plates.Seen;
            content.Add(Label(Loc.F("laptop.collection.plates", PlateMath.Collected(seen), PlateMath.Count), "laptop-heading"));
            var wall = Card(Loc.T("laptop.collection.wall"));
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            wall.Add(row);
            for (int i = 0; i < PlateMath.Count; i++)
            {
                var plate = Label(PlateMath.Has(seen, i) ? Loc.T($"plate.{i}") : "???", PlateMath.Has(seen, i) ? "laptop-line" : "laptop-muted");
                plate.style.width = 170;
                plate.style.marginRight = 6;
                row.Add(plate);
            }

            var cat = Card(Loc.F("laptop.collection.cat", StationProfile.CatName));
            cat.Add(Label(Loc.T("laptop.collection.catHint"), "laptop-muted"));
            var field = new TextField { maxLength = StationProfile.MaxNameLength, value = StationProfile.CustomCatName ?? string.Empty };
            field.AddToClassList("name-field");
            cat.Add(field);
            Action(Actions(cat), Loc.T("laptop.collection.rename"), () => StationProfile.SetCatName(field.value));

            var photo = Card(Loc.T("laptop.collection.photoTitle"));
            photo.Add(Label(Loc.T("laptop.collection.photo"), "laptop-line"));
        }

        private void BuildSkills(VisualElement content)
        {
            int learned = HudModel.Skills.Learned;
            int points = SkillMath.FreePoints(HudModel.Level.Level, learned);
            content.Add(Label(Loc.F("laptop.skills.heading", points), "laptop-heading"));
            for (int branch = 0; branch < OwnerSkills.Count / OwnerSkills.PerBranch; branch++)
            {
                var card = Card(Loc.T($"skill.branch.{branch}"));
                for (int i = 0; i < OwnerSkills.PerBranch; i++)
                {
                    var skill = (OwnerSkill)(branch * OwnerSkills.PerBranch + i);
                    bool has = SkillMath.Has(learned, skill);
                    string mark = has ? "✔ " : SkillMath.Unlocked(learned, skill) ? "○ " : "× ";
                    card.Add(Label($"{mark}{Loc.T($"skill.{skill}")}: {Loc.T($"skill.{skill}.desc")}", has ? "laptop-line" : "laptop-muted"));
                    if (has)
                        continue;

                    var learnSkill = skill;
                    var button = Action(Actions(card), Loc.F("laptop.skills.learn", Loc.T($"skill.{skill}")), () => StationCommands.LearnSkill(learnSkill));
                    button.SetEnabled(SkillMath.CanLearn(learned, skill, HudModel.Level.Level));
                }
            }
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
                string status = regular.Lost ? Loc.T("panel.regulars.lost") : GameTexts.Hearts(regular.Loyalty);
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
                ? Loc.F("panel.finance.rating", GameTexts.Stars(Mathf.RoundToInt(rating)), rating, stats.RatingCount)
                : Loc.T("panel.finance.noRating"), "laptop-line"));

            if (HudModel.ChargersOpen > 0)
                station.Add(Label(Loc.F("laptop.stats.chargers", HudModel.ChargersUsed, HudModel.ChargersOpen), "laptop-line"));

            var reviews = Card(Loc.T("panel.finance.reviews"));
            if (HudModel.Reviews.Count == 0)
                reviews.Add(Label(Loc.T("laptop.stats.noReviews"), "laptop-muted"));
            foreach (var review in HudModel.Reviews)
            {
                string author = review.RegularId > 0 ? GameTexts.RegularName(review.RegularId) + ": " : string.Empty;
                reviews.Add(Label($"{GameTexts.Stars(review.Stars)}  {author}{Loc.T($"review.{review.Stars}.{review.Variant}")}", "laptop-line"));
            }
        }
    }
}
