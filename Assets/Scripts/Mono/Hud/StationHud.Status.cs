using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Mono.Hud;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>Always-visible HUD blocks: status, fuel, pumps, quest and the center message.</summary>
    public partial class StationHud
    {
        private string BuildQuest()
        {
            var quest = HudModel.Quest;
            _builder.Clear();
            _builder.AppendLine(Loc.F("quest.header", GameTexts.QuestTitle(quest)));
            _builder.AppendLine(GameTexts.QuestGoalText(quest, HudModel.QuestProgress));
            _builder.Append(Loc.F("quest.reward", GameTexts.QuestReward(quest)));
            return _builder.ToString();
        }

        private string BuildStatus()
        {
            var economy = HudModel.Economy;
            int hours = (int)HudModel.Hour;
            int minutes = (int)((HudModel.Hour - hours) * 60f);

            _builder.Clear();
            _builder.AppendLine(StationProfile.DisplayName);
            _builder.AppendLine(Loc.F("hud.day", HudModel.Day, hours, minutes) + Loc.F("hud.speed", GameSettings.GameSpeed));
            _builder.AppendLine(Loc.F("hud.money", economy.Money));
            _builder.AppendLine(Loc.F("hud.reputation", economy.Reputation * 100f));
            _builder.AppendLine(Loc.F("hud.cleanliness", HudModel.Cleanliness.Value * 100f, HudModel.Cleanliness.TrashCount));
            if (HudModel.Workers.Count > 0)
            {
                float wages = 0f;
                foreach (var worker in HudModel.Workers)
                    wages += worker.Wage;
                _builder.AppendLine(Loc.F("hud.staff", HudModel.Workers.Count, wages));
            }

            var level = HudModel.Level;
            _builder.AppendLine(level.Level >= ProgressMath.MaxLevel
                ? Loc.F("hud.level.max", level.Level)
                : Loc.F("hud.level", level.Level, level.Experience, ProgressMath.ExperienceToNext(level.Level)));

            if (HudModel.Campaign.Active)
            {
                var goal = CampaignMath.Goal(HudModel.Campaign.Chapter);
                _builder.AppendLine(Loc.F("hud.campaign", HudModel.Campaign.Chapter, HudModel.Campaign.Repaid, goal.Repaid, goal.Deadline));
            }

            _builder.AppendLine(Loc.F("hud.stars", GameTexts.Stars(HudModel.Stars.Stars)));
            _builder.AppendLine(Loc.F("hud.season", Loc.T($"season.{HudModel.Season.Season}"), SeasonMath.DayOfSeason(HudModel.Day),
                SeasonMath.DaysPerSeason, Loc.T($"weather.{HudModel.Season.Weather}")));
            if (HudModel.Road.Kind != RoadEventKind.None)
                _builder.AppendLine(Loc.F("hud.road", Loc.T($"road.{HudModel.Road.Kind}"), HudModel.Road.HoursLeft));
            if (HudModel.Hosted.Active != HostedEventKind.None)
                _builder.AppendLine(Loc.F("hud.hosted", Loc.T($"hosted.{HudModel.Hosted.Active}"), HudModel.Hosted.Attendees));
            if (HudModel.World.Active != WorldEventKind.None)
                _builder.AppendLine(Loc.F("hud.event", Loc.T($"event.{HudModel.World.Active}"), HudModel.World.HoursLeft));

            _builder.AppendLine(Loc.F("hud.today", economy.DayIncome, economy.DayExpenses));
            _builder.Append(Loc.F("hud.served", economy.DayServed, economy.DayLost));
            return _builder.ToString();
        }

        private string BuildFuel()
        {
            _builder.Clear();
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = HudModel.Fuel[i];
                if ((int)_selectedFuel == i)
                    _builder.Append("▶ ");
                _builder.Append(Loc.F("hud.fuel", GameTexts.FuelName((FuelType)i), fuel.Amount, fuel.Capacity, fuel.SellPrice, fuel.MarketPrice));
                if (HudModel.PendingDelivery[i] > 0f)
                    _builder.Append(Loc.F("hud.fuel.pending", HudModel.PendingDelivery[i]));
                if (i < FuelTypes.Count - 1)
                    _builder.AppendLine();
            }

            return _builder.ToString();
        }

        private string BuildPumps()
        {
            _builder.Clear();
            _builder.AppendLine(Loc.F("hud.queue", HudModel.QueueLength, HudModel.CarsOnSite));

            // Facilities that are not built yet stay off the HUD: the upgrades panel lists them.
            if (HudModel.HasWash && HudModel.Upgrades.CarWash > 0)
            {
                string wash = HudModel.WashTimeLeft > 0f ? Loc.F("hud.wash.working", HudModel.WashTimeLeft)
                    : HudModel.WashBusy ? Loc.T("hud.wash.arriving")
                    : Loc.T("hud.free.f");
                _builder.AppendLine(Loc.F("hud.wash", wash));
            }

            if (HudModel.HasParking && HudModel.ParkingOpen > 0)
                _builder.AppendLine(Loc.F("hud.parking", HudModel.ParkingUsed, HudModel.ParkingOpen));

            if (HudModel.HasTireService && HudModel.Upgrades.TireService > 0)
            {
                string tires = HudModel.TireTimeLeft > 0f ? Loc.F("hud.tires.working", HudModel.TireTimeLeft)
                    : HudModel.TireCarWaiting ? Loc.T("hud.tires.waiting")
                    : Loc.T("hud.free.m");
                _builder.AppendLine(Loc.F("hud.tires", tires));
            }

            if (HudModel.HasMotel && HudModel.MotelOpen > 0)
            {
                _builder.Append(Loc.F("hud.motel", HudModel.MotelUsed, HudModel.MotelOpen));
                if (HudModel.MotelDirty > 0)
                    _builder.Append(Loc.F("hud.motel.dirty", HudModel.MotelDirty));
                _builder.AppendLine();
            }

            if (HudModel.HasRestroom)
            {
                _builder.AppendLine(HudModel.RestroomDirt >= FacilityMath.RestroomDisgustingDirt
                    ? Loc.T("hud.restroom.disgusting")
                    : Loc.F("hud.restroom", HudModel.RestroomDirt * 100f));
            }

            foreach (var pump in HudModel.Pumps)
            {
                if (pump.Locked)
                    continue;

                _builder.Append(Loc.F("hud.pump", pump.Number));

                if (pump.Condition <= 0f && !pump.Occupied)
                {
                    _builder.AppendLine(Loc.T("hud.pump.broken"));
                    continue;
                }

                string wear = pump.Condition < ProgressMath.RepairThreshold
                    ? Loc.F("hud.pump.wear", 100f - pump.Condition * 100f)
                    : string.Empty;
                if (!pump.Occupied)
                {
                    _builder.AppendLine(Loc.T("hud.free.f") + wear);
                    continue;
                }

                _builder.AppendLine(Loc.F("hud.pump.car", GameTexts.CustomerName(pump.Customer), GameTexts.CarStateName(pump.CarState),
                    GameTexts.FuelName(pump.FuelType), pump.ReceivedLiters, pump.RequestedLiters, pump.PatienceRatio * 100f, wear));
            }

            return _builder.ToString().TrimEnd();
        }

        private string BuildCenter()
        {
            var report = HudModel.LastReport;
            if (report.Day > 0 && report.Day != _shownReportDay)
            {
                _shownReportDay = report.Day;
                _reportShownAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - _reportShownAt < ReportDuration)
            {
                return Loc.F("hud.report", report.Day, report.Income, report.Expenses, report.Income - report.Expenses,
                    report.Served, report.Lost);
            }

            if (HudModel.Message != null && Time.unscaledTime - HudModel.MessageTime < MessageDuration)
                return HudModel.Message;

            // Interaction prompts are for walking around; build mode aims with the mouse instead.
            if (BuildMode.Active)
                return string.Empty;

            return HudModel.Hint switch
            {
                InteractionHint.None => string.Empty,
                InteractionHint.PumpFree => string.Empty,
                InteractionHint.Repair => Loc.F("hint.Repair", ProgressMath.RepairStepCost),
                InteractionHint.Renovate => Loc.F("hint.Renovate", Loc.T($"renovation.{HudModel.NearRenovationKind}"),
                    RenovationMath.Cost(HudModel.NearRenovationKind), RenovationMath.RequiredLevel(HudModel.NearRenovationKind)),
                _ => Loc.T($"hint.{HudModel.Hint}")
            };
        }
    }
}
