using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>
    /// Applies commands queued by the UI (StationCommands). This file dispatches them and handles fuel, upgrades,
    /// paint and save/load; the other StationCommandSystem.*.cs files hold the commands of each area.
    /// </summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial class StationCommandSystem : SystemBase
    {
        private const float MinPrice = 0.05f;

        protected override void OnCreate()
        {
            RequireForUpdate<Economy>();
            RequireForUpdate<StationSettings>();
            RequireForUpdate<StationUpgrades>();
        }

        protected override void OnUpdate()
        {
            var station = SystemAPI.GetSingletonEntity<Economy>();
            while (StationCommands.TryDequeue(out var command))
            {
                switch (command.Type)
                {
                    case StationCommandType.ChangePrice:
                        ChangePrice(station, command.Fuel, command.Value);
                        break;
                    case StationCommandType.OrderFuel:
                        OrderFuel(station, command.Fuel, command.Value);
                        break;
                    case StationCommandType.BuyUpgrade:
                        BuyUpgrade(station, command.Upgrade);
                        break;
                    case StationCommandType.SaveGame:
                        SaveService.Write(SaveService.Capture(EntityManager, station));
                        HudModel.Notify(Loc.T("msg.saved"));
                        break;
                    case StationCommandType.LoadGame:
                        if (SaveService.TryRead(out var data))
                        {
                            SaveService.Apply(EntityManager, station, data);
                            HudModel.Notify(Loc.T("msg.loaded"));
                        }
                        else
                        {
                            HudModel.Notify(Loc.T("msg.noSave"));
                        }
                        break;
                    case StationCommandType.ChangeProductPrice:
                        ChangeProductPrice(command.Product, command.Value);
                        break;
                    case StationCommandType.OrderProducts:
                        OrderProducts(station, command.Product, (int)command.Value);
                        break;
                    case StationCommandType.PaintStation:
                        PaintStation(station, (int)command.Value);
                        break;
                    case StationCommandType.HireCandidate:
                        HireCandidate(station, (int)command.Value);
                        break;
                    case StationCommandType.FireWorker:
                        FireWorker(station, (int)command.Value);
                        break;
                    case StationCommandType.NewGame:
                        SaveService.Delete();
                        if (SaveService.Defaults != null)
                            SaveService.Apply(EntityManager, station, SaveService.Defaults);
                        StartMode(station, (Difficulty)(int)command.Value, command.Mode);
                        HudModel.Notify(Loc.T("msg.newGame"));
                        break;
                    case StationCommandType.TakeLoan:
                        TakeLoan(station, (LoanKind)(int)command.Value);
                        break;
                    case StationCommandType.RepayLoan:
                        RepayLoan(station);
                        break;
                    case StationCommandType.ToggleInsurance:
                        ToggleInsurance(station);
                        break;
                    case StationCommandType.BuyOutCompetitor:
                        BuyOutCompetitor(station);
                        break;
                    case StationCommandType.PlaceProp:
                        PlaceProp(station, command.Prop, command.Position, command.Value);
                        break;
                    case StationCommandType.RemoveProp:
                        RemoveProp(station, command.Position);
                        break;
                    case StationCommandType.RepayUncleDebt:
                        RepayUncleDebt(station, command.Value);
                        break;
                    case StationCommandType.AcknowledgeVictory:
                        if (SystemAPI.HasComponent<Campaign>(station))
                            SystemAPI.GetComponentRW<Campaign>(station).ValueRW.VictoryShown = true;
                        break;
                    case StationCommandType.AnswerBuyoutOffer:
                        AnswerBuyoutOffer(station, command.Value > 0.5f);
                        break;
                    case StationCommandType.LearnSkill:
                        LearnSkill(station, (OwnerSkill)(int)command.Value);
                        break;
                    case StationCommandType.PlanEvent:
                        PlanEvent(station, (HostedEventKind)(int)command.Value);
                        break;
                    case StationCommandType.TogglePromo:
                        TogglePromo(command.Product);
                        break;
                    case StationCommandType.OrderIngredients:
                        OrderIngredients(station);
                        break;
                    case StationCommandType.SetSupplier:
                        SetSupplier((SupplierKind)(int)command.Value);
                        break;
                    case StationCommandType.PraiseWorker:
                    case StationCommandType.TrainWorker:
                    case StationCommandType.RaiseWage:
                    case StationCommandType.ToggleShift:
                        ManageWorker(station, command.Type, (int)command.Value);
                        break;
                    case StationCommandType.AcceptContract:
                        AcceptContract(station, (int)command.Value);
                        break;
                    case StationCommandType.DeclineContract:
                        DeclineContract(station, (int)command.Value);
                        break;
                    case StationCommandType.CancelContract:
                        CancelContract(station, (int)command.Value);
                        break;
                }
            }
        }

        private void ChangePrice(Entity station, FuelType fuel, float delta)
        {
            var stock = SystemAPI.GetBuffer<FuelStock>(station);
            var entry = stock[(int)fuel];
            entry.SellPrice = math.max(MinPrice, entry.SellPrice + delta);
            stock[(int)fuel] = entry;
        }

        private void OrderFuel(Entity station, FuelType fuel, float liters)
        {
            var stock = SystemAPI.GetBuffer<FuelStock>(station)[(int)fuel];

            float pending = 0f;
            foreach (var delivery in SystemAPI.Query<RefRO<FuelDelivery>>())
            {
                if (delivery.ValueRO.Type == fuel)
                    pending += delivery.ValueRO.Liters;
            }

            liters = math.min(liters, stock.Capacity - stock.Amount - pending);
            if (liters < 1f)
            {
                HudModel.Notify(Loc.T("msg.tankFull"));
                return;
            }

            var economy = SystemAPI.GetSingletonRW<Economy>();
            float cost = liters * stock.BuyPrice * SkillMath.FuelPriceFactor((SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0));
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;

            var settings = SystemAPI.GetSingleton<StationSettings>();
            var order = EntityManager.CreateEntity();
            EntityManager.AddComponentData(order, new FuelDelivery
            {
                Type = fuel,
                Liters = liters,
                TimeLeft = settings.FuelDeliveryTime
            });

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.FuelOrdered, fuel, liters);
            HudModel.Notify(Loc.F("msg.fuelOrdered", liters, GameTexts.FuelName(fuel), settings.FuelDeliveryTime));
        }

        private void PaintStation(Entity station, int scheme)
        {
            if (!SystemAPI.HasComponent<StationStyle>(station) || scheme < 1 || scheme >= StyleMath.SchemeCount)
                return;

            if (SystemAPI.GetComponent<StationStyle>(station).Scheme == scheme)
            {
                HudModel.Notify(Loc.T("msg.alreadyPainted"));
                return;
            }

            int requiredLevel = StyleMath.RequiredLevel(scheme);
            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (stationLevel < requiredLevel)
            {
                HudModel.Notify(Loc.F("msg.schemeNeedsLevel", GameTexts.SchemeName(scheme), requiredLevel));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = StyleMath.Cost(scheme);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            SystemAPI.SetComponent(station, new StationStyle { Scheme = scheme });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.StationPainted, default, scheme);
            HudModel.Notify(Loc.F("msg.painted", GameTexts.SchemeName(scheme)));
        }

        private void BuyUpgrade(Entity station, UpgradeType type)
        {
            var upgrades = SystemAPI.GetComponentRW<StationUpgrades>(station);
            int level = upgrades.ValueRO.Get(type);
            if (!UpgradeMath.CanUpgrade(type, level))
            {
                HudModel.Notify(Loc.F("msg.upgradeMax", GameTexts.UpgradeName(type)));
                return;
            }

            int requiredLevel = ProgressMath.RequiredLevel(type, level);
            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (stationLevel < requiredLevel)
            {
                HudModel.Notify(Loc.F("msg.upgradeNeedsLevel", GameTexts.UpgradeName(type), requiredLevel));
                return;
            }

            // The EV charging licence also needs a three-star station.
            int stars = SystemAPI.HasComponent<StationStars>(station) ? SystemAPI.GetComponent<StationStars>(station).Stars : StarMath.MaxStars;
            if (type == UpgradeType.EvCharger && stars < StarMath.EvLicenceStars)
            {
                HudModel.Notify(Loc.F("msg.needStars", GameTexts.UpgradeName(type), StarMath.EvLicenceStars));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = UpgradeMath.Cost(type, level);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            upgrades.ValueRW.Set(type, level + 1);

            switch (type)
            {
                case UpgradeType.TankCapacity:
                    var stock = SystemAPI.GetBuffer<FuelStock>(station);
                    for (int i = 0; i < stock.Length; i++)
                    {
                        var entry = stock[i];
                        entry.Capacity += UpgradeMath.TankBonusPerLevel;
                        stock[i] = entry;
                    }
                    break;
                case UpgradeType.SupplyManager:
                    economy.ValueRW.DailyFixedCosts += FacilityMath.SupplyManagerSalaryPerLevel;
                    break;
            }

            HudModel.Notify(Loc.F("msg.upgradeBought", GameTexts.UpgradeName(type), level + 1));
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.UpgradeBought, default, cost);
        }
    }
}
