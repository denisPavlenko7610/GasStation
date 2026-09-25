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
    /// <summary>Applies commands queued by the UI: prices, fuel orders, upgrades, save/load.</summary>
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
                        if (SystemAPI.HasComponent<StationRules>(station))
                            SystemAPI.SetComponent(station, new StationRules { Difficulty = (Difficulty)(int)command.Value });
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
                    case StationCommandType.TogglePromo:
                        TogglePromo(command.Product);
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

        private void TogglePromo(ProductType product)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            var shelf = shelves[(int)product];
            shelf.Promo = !shelf.Promo;
            shelves[(int)product] = shelf;
            HudModel.Notify(Loc.F(shelf.Promo ? "msg.promoOn" : "msg.promoOff", GameTexts.ProductName(product)));
        }

        private void SetSupplier(SupplierKind supplier)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shopEntity = SystemAPI.GetSingletonEntity<Shop>();
            var shop = SystemAPI.GetComponent<Shop>(shopEntity);
            shop.Supplier = supplier;
            SystemAPI.SetComponent(shopEntity, shop);
            HudModel.Notify(Loc.F("msg.supplier", Loc.T($"supplier.{supplier}")));
        }

        private void ManageWorker(Entity station, StationCommandType action, int workerId)
        {
            int today = SystemAPI.HasComponent<GameTime>(station) ? SystemAPI.GetComponent<GameTime>(station).Day : 0;
            foreach (var worker in SystemAPI.Query<RefRW<Worker>>())
            {
                if (worker.ValueRO.Id != workerId)
                    continue;

                ref var w = ref worker.ValueRW;
                string name = GameTexts.StaffName(w.NameIndex);
                switch (action)
                {
                    case StationCommandType.PraiseWorker:
                        if (w.PraisedDay == today)
                        {
                            HudModel.Notify(Loc.F("msg.alreadyPraised", name));
                            return;
                        }

                        w.PraisedDay = today;
                        w.Mood = math.saturate(w.Mood + StaffMath.PraiseMood);
                        HudModel.Notify(Loc.F("msg.praised", name));
                        break;

                    case StationCommandType.TrainWorker:
                    {
                        if (!StaffMath.CanTrain(w))
                        {
                            HudModel.Notify(Loc.F("msg.trainingMax", name));
                            return;
                        }

                        float cost = StaffMath.TrainingCost(w.Training);
                        var economy = SystemAPI.GetComponentRW<Economy>(station);
                        if (economy.ValueRO.Money < cost)
                        {
                            HudModel.Notify(Loc.F("msg.noMoney", cost));
                            return;
                        }

                        economy.ValueRW.Money -= cost;
                        economy.ValueRW.DayExpenses += cost;
                        w.Training++;
                        w.Skill = math.min(StaffMath.MaxSkill, w.Skill + StaffMath.TrainingSkill);
                        HudModel.Notify(Loc.F("msg.trained", name, w.Skill * 100f));
                        break;
                    }

                    case StationCommandType.RaiseWage:
                        w.Wage = StaffMath.Raise(w.Wage);
                        w.Mood = math.saturate(w.Mood + StaffMath.PraiseMood);
                        HudModel.Notify(Loc.F("msg.raised", name, w.Wage));
                        break;

                    case StationCommandType.ToggleShift:
                        w.Shift = w.Shift == WorkShift.Day ? WorkShift.Night : WorkShift.Day;
                        HudModel.Notify(Loc.F("msg.shiftChanged", name, Loc.T($"shift.{w.Shift}")));
                        break;
                }

                return;
            }
        }

        private void AcceptContract(Entity station, int offerId)
        {
            if (!SystemAPI.HasBuffer<ContractOffer>(station) || !SystemAPI.HasBuffer<Contract>(station))
                return;

            var offers = SystemAPI.GetBuffer<ContractOffer>(station);
            var contracts = SystemAPI.GetBuffer<Contract>(station);
            int index = -1;
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Id == offerId)
                    index = i;
            }

            if (index < 0)
                return;

            if (contracts.Length >= ContractMath.MaxActive)
            {
                HudModel.Notify(Loc.F("msg.contractsFull", ContractMath.MaxActive));
                return;
            }

            var offer = offers[index];
            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            int required = ContractMath.Get(offer.Type).RequiredLevel;
            if (stationLevel < required)
            {
                HudModel.Notify(Loc.F("msg.contractNeedsLevel", required));
                return;
            }

            offers.RemoveAt(index);
            contracts.Add(new Contract
            {
                Id = offer.Id,
                Type = offer.Type,
                Price = offer.Price,
                DaysLeft = offer.Days,
                LastSentDay = -1,
                LastSentHour = -1
            });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.ContractAccepted, default, (float)offer.Type, offer.Id);
        }

        private void DeclineContract(Entity station, int offerId)
        {
            if (!SystemAPI.HasBuffer<ContractOffer>(station))
                return;

            var offers = SystemAPI.GetBuffer<ContractOffer>(station);
            for (int i = offers.Length - 1; i >= 0; i--)
            {
                if (offers[i].Id == offerId)
                    offers.RemoveAt(i);
            }
        }

        /// <summary>Walking away from a contract costs three missed-vehicle penalties and some reputation.</summary>
        private void CancelContract(Entity station, int contractId)
        {
            if (!SystemAPI.HasBuffer<Contract>(station))
                return;

            var contracts = SystemAPI.GetBuffer<Contract>(station);
            for (int i = 0; i < contracts.Length; i++)
            {
                if (contracts[i].Id != contractId)
                    continue;

                var contract = contracts[i];
                float penalty = ContractMath.CancelPenalty(contract.Type);
                var economy = SystemAPI.GetComponentRW<Economy>(station);
                economy.ValueRW.Money -= penalty;
                economy.ValueRW.DayExpenses += penalty;
                economy.ValueRW.Reputation = StationMath.ClampReputation(economy.ValueRO.Reputation - ContractMath.CancelReputation);
                contracts.RemoveAt(i);
                HudModel.Notify(Loc.F("msg.contractWalkedAway", GameTexts.ContractName(contract.Type), penalty));
                return;
            }
        }

        private void PlaceProp(Entity station, PropType type, float3 position, float yaw)
        {
            if (!SystemAPI.HasSingleton<BuildArea>())
                return;

            var areaEntity = SystemAPI.GetSingletonEntity<BuildArea>();
            var area = SystemAPI.GetComponent<BuildArea>(areaEntity);
            position = PropMath.Snap(position);

            var zones = new List<NoBuildZone>();
            foreach (var zone in SystemAPI.GetBuffer<NoBuildZone>(areaEntity))
                zones.Add(zone);
            var others = new List<float2>();
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
                others.Add(prop.ValueRO.Position.xz);

            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            var info = PropMath.Get(type);
            var error = PropMath.Check(type, position.xz, area, zones, others, stationLevel, economy.ValueRO.Money);
            if (error != PlacementError.None)
            {
                HudModel.Notify(error switch
                {
                    PlacementError.NeedsLevel => Loc.F("msg.propNeedsLevel", GameTexts.PropName(type), info.RequiredLevel),
                    PlacementError.NoMoney => Loc.F("msg.noMoney", info.Cost),
                    _ => Loc.T($"build.error.{error}")
                });
                return;
            }

            economy.ValueRW.Money -= info.Cost;
            economy.ValueRW.DayExpenses += info.Cost;

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new PlacedProp
            {
                Id = area.NextPropId++,
                Type = type,
                Position = position,
                Yaw = yaw
            });
            SystemAPI.SetComponent(areaEntity, area);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.PropPlaced, default, (int)type);
        }

        private void RemoveProp(Entity station, float3 position)
        {
            var nearest = Entity.Null;
            var nearestType = PropType.TrashBin;
            float best = PropMath.PickRadius * PropMath.PickRadius;
            foreach (var (prop, entity) in SystemAPI.Query<RefRO<PlacedProp>>().WithEntityAccess())
            {
                float distance = math.distancesq(prop.ValueRO.Position.xz, position.xz);
                if (distance > best)
                    continue;
                best = distance;
                nearest = entity;
                nearestType = prop.ValueRO.Type;
            }

            if (nearest == Entity.Null)
                return;

            float refund = PropMath.Refund(nearestType);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            economy.ValueRW.Money += refund;
            EntityManager.DestroyEntity(nearest);
            HudModel.Notify(Loc.F("msg.propRemoved", GameTexts.PropName(nearestType), refund));
        }

        private void TakeLoan(Entity station, LoanKind kind)
        {
            if (!SystemAPI.HasComponent<Finance>(station) || kind == LoanKind.None)
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            if (finance.ValueRO.Loan != LoanKind.None)
            {
                HudModel.Notify(Loc.T("msg.loanAlready"));
                return;
            }

            float amount = FinanceMath.LoanAmount(kind);
            finance.ValueRW.Loan = kind;
            finance.ValueRW.LoanBalance = FinanceMath.LoanTotal(kind);
            finance.ValueRW.WeeklyPayment = FinanceMath.LoanWeeklyPayment(kind);

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            economy.ValueRW.Money += amount;

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.LoanTaken, default, amount);
            HudModel.Notify(Loc.F("msg.loanTaken", amount, FinanceMath.LoanWeeklyPayment(kind), FinanceMath.LoanWeeks(kind)));
        }

        private void RepayLoan(Entity station)
        {
            if (!SystemAPI.HasComponent<Finance>(station))
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            if (finance.ValueRO.Loan == LoanKind.None)
            {
                HudModel.Notify(Loc.T("msg.noLoan"));
                return;
            }

            float cost = FinanceMath.EarlyRepayment(finance.ValueRO.LoanBalance, finance.ValueRO.Loan);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            finance.ValueRW.Loan = LoanKind.None;
            finance.ValueRW.LoanBalance = 0f;
            finance.ValueRW.WeeklyPayment = 0f;

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.LoanRepaid, default, cost);
            HudModel.Notify(Loc.F("msg.loanRepaid", cost));
        }

        private void ToggleInsurance(Entity station)
        {
            if (!SystemAPI.HasComponent<Finance>(station))
                return;

            var finance = SystemAPI.GetComponentRW<Finance>(station);
            finance.ValueRW.Insured = !finance.ValueRO.Insured;
            HudModel.Notify(finance.ValueRO.Insured
                ? Loc.F("msg.insuranceOn", FinanceMath.InsurancePremium)
                : Loc.T("msg.insuranceOff"));
        }

        private void BuyOutCompetitor(Entity station)
        {
            if (!SystemAPI.HasComponent<Competitor>(station))
                return;

            var rival = SystemAPI.GetComponentRW<Competitor>(station);
            if (!rival.ValueRO.Active || rival.ValueRO.BoughtOut)
                return;

            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (stationLevel < CompetitionMath.BuyoutLevel)
            {
                HudModel.Notify(Loc.F("msg.buyoutNeedsLevel", CompetitionMath.BuyoutLevel));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < CompetitionMath.BuyoutPrice)
            {
                HudModel.Notify(Loc.F("msg.noMoney", CompetitionMath.BuyoutPrice));
                return;
            }

            economy.ValueRW.Money -= CompetitionMath.BuyoutPrice;
            economy.ValueRW.DayExpenses += CompetitionMath.BuyoutPrice;
            rival.ValueRW.BoughtOut = true;
            rival.ValueRW.Promo = CompetitorPromo.None;
            rival.ValueRW.OurShare = 1f;

            // Regulars who left for PetroMax have nowhere else to go now.
            if (SystemAPI.HasBuffer<RegularState>(station))
            {
                var regulars = SystemAPI.GetBuffer<RegularState>(station);
                for (int i = 0; i < regulars.Length; i++)
                {
                    var regular = regulars[i];
                    if (!regular.Lost)
                        continue;
                    regular.Lost = false;
                    regular.Upsets = 0;
                    regular.Loyalty = VisitorMath.StartLoyalty;
                    regulars[i] = regular;
                }
            }

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.CompetitorBoughtOut, default, CompetitionMath.BuyoutPrice);
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
            float cost = liters * stock.BuyPrice;
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

        private void ChangeProductPrice(ProductType product, float delta)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            var shelf = shelves[(int)product];
            shelf.SellPrice = math.max(MinPrice, shelf.SellPrice + delta);
            shelves[(int)product] = shelf;
        }

        private void OrderProducts(Entity station, ProductType product, int count)
        {
            if (!SystemAPI.HasSingleton<Shop>())
            {
                HudModel.Notify(Loc.T("msg.noShop"));
                return;
            }

            var shop = SystemAPI.GetSingleton<Shop>();
            var shelf = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>())[(int)product];

            int pending = 0;
            foreach (var delivery in SystemAPI.Query<RefRO<ProductDelivery>>())
            {
                if (delivery.ValueRO.Type == product)
                    pending += delivery.ValueRO.Count;
            }

            count = math.min(count, shelf.Capacity - shelf.Stock - pending);
            if (count <= 0)
            {
                HudModel.Notify(Loc.F("msg.shelvesFull", GameTexts.ProductName(product)));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = math.round(count * shelf.BuyPrice * ShopMath.SupplierPriceFactor(shop.Supplier) * 100f) / 100f;
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;

            float time = shop.DeliveryTime * ShopMath.SupplierTimeFactor(shop.Supplier);
            var order = EntityManager.CreateEntity();
            EntityManager.AddComponentData(order, new ProductDelivery { Type = product, Count = count, TimeLeft = time, Supplier = shop.Supplier });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.ProductsOrdered, default, count);
            HudModel.Notify(Loc.F("msg.productsOrdered", GameTexts.ProductName(product), count, time));
        }

        private void HireCandidate(Entity station, int index)
        {
            if (!SystemAPI.HasComponent<StaffRoster>(station))
                return;

            var candidates = SystemAPI.GetBuffer<StaffCandidate>(station);
            if (index < 0 || index >= candidates.Length)
                return;

            if (SystemAPI.GetComponent<StaffPower>(station).Headcount >= StaffMath.MaxStaff)
            {
                HudModel.Notify(Loc.F("msg.staffFull", StaffMath.MaxStaff));
                return;
            }

            var candidate = candidates[index];
            float fee = StaffMath.HiringFee(candidate.Wage);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < fee)
            {
                HudModel.Notify(Loc.F("msg.noMoneyHire", fee));
                return;
            }

            economy.ValueRW.Money -= fee;
            economy.ValueRW.DayExpenses += fee;
            candidates.RemoveAt(index);

            var roster = SystemAPI.GetComponent<StaffRoster>(station);
            int id = roster.NextId++;
            SystemAPI.SetComponent(station, roster);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.WorkerHired, default, id);

            // New people fill the emptier shift of their role, so nights are covered too.
            int dayShift = 0, nightShift = 0;
            foreach (var other in SystemAPI.Query<RefRO<Worker>>())
            {
                if (other.ValueRO.Role != candidate.Role)
                    continue;
                if (other.ValueRO.Shift == WorkShift.Day)
                    dayShift++;
                else
                    nightShift++;
            }

            var worker = EntityManager.CreateEntity();
            EntityManager.AddComponentData(worker, new Worker
            {
                Id = id,
                Role = candidate.Role,
                Skill = candidate.Skill,
                Wage = candidate.Wage,
                Honesty = candidate.Honesty,
                NameIndex = candidate.NameIndex,
                Trait = candidate.Trait,
                Shift = dayShift > nightShift ? WorkShift.Night : WorkShift.Day,
                Energy = 1f,
                Mood = StaffMath.StartMood,
                PraisedDay = -1
            });

            HudModel.Notify(Loc.F("msg.hired", GameTexts.RoleName(candidate.Role), GameTexts.StaffName(candidate.NameIndex)));
        }

        private void FireWorker(Entity station, int id)
        {
            foreach (var (worker, entity) in SystemAPI.Query<RefRO<Worker>>().WithEntityAccess())
            {
                if (worker.ValueRO.Id != id)
                    continue;

                string name = GameTexts.StaffName(worker.ValueRO.NameIndex);
                var role = worker.ValueRO.Role;
                StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.WorkerFired, default, id);
                EntityManager.DestroyEntity(entity);
                HudModel.Notify(Loc.F("msg.fired", GameTexts.RoleName(role), name));
                return;
            }
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
