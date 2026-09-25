using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Copies simulation state and events into HudModel for the MonoBehaviour UI and audio.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HudBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            HudModel.Events.Clear();
            HudModel.HasStation = SystemAPI.HasSingleton<Economy>() && SystemAPI.HasSingleton<GameTime>();
            if (!HudModel.HasStation)
                return;

            var time = SystemAPI.GetSingleton<GameTime>();
            HudModel.Hour = time.Hour;
            HudModel.Day = time.Day;
            HudModel.Economy = SystemAPI.GetSingleton<Economy>();
            HudModel.LastReport = SystemAPI.GetSingleton<DayReport>();
            HudModel.Upgrades = SystemAPI.GetSingleton<StationUpgrades>();
            if (SystemAPI.HasSingleton<StationLevel>())
                HudModel.Level = SystemAPI.GetSingleton<StationLevel>();
            if (SystemAPI.HasSingleton<WorldEvents>())
                HudModel.World = SystemAPI.GetSingleton<WorldEvents>();
            if (SystemAPI.HasSingleton<StationCleanliness>())
                HudModel.Cleanliness = SystemAPI.GetSingleton<StationCleanliness>();

            if (SystemAPI.HasSingleton<Finance>())
                HudModel.Finance = SystemAPI.GetSingleton<Finance>();
            if (SystemAPI.HasSingleton<StationRules>())
                HudModel.Difficulty = SystemAPI.GetSingleton<StationRules>().Difficulty;
            if (SystemAPI.HasSingleton<Competitor>())
                HudModel.Competitor = SystemAPI.GetSingleton<Competitor>();
            HudModel.GameOver = HudModel.Finance.Bankrupt;

            HudModel.Regulars.Clear();
            if (SystemAPI.HasSingleton<RegularState>())
            {
                foreach (var regular in SystemAPI.GetSingletonBuffer<RegularState>(true))
                    HudModel.Regulars.Add(regular);
            }
            HudModel.Buzz = SystemAPI.HasSingleton<Buzz>() ? SystemAPI.GetSingleton<Buzz>() : default;

            HudModel.Offers.Clear();
            if (SystemAPI.HasSingleton<ContractOffer>())
            {
                foreach (var offer in SystemAPI.GetSingletonBuffer<ContractOffer>(true))
                    HudModel.Offers.Add(offer);
            }
            HudModel.Contracts.Clear();
            if (SystemAPI.HasSingleton<Contract>())
            {
                foreach (var contract in SystemAPI.GetSingletonBuffer<Contract>(true))
                    HudModel.Contracts.Add(contract);
            }

            HudModel.StaffBodies.Clear();
            foreach (var (agent, transform, path) in SystemAPI.Query<RefRO<StaffAgent>, RefRO<LocalTransform>, DynamicBuffer<PathPoint>>())
            {
                HudModel.StaffBodies.Add(new StaffBody
                {
                    WorkerId = agent.ValueRO.WorkerId,
                    Role = agent.ValueRO.Role,
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation,
                    Job = agent.ValueRO.Job,
                    Away = agent.ValueRO.Task == AgentTask.OffDuty && path.IsEmpty
                });
            }

            HudModel.Season = SystemAPI.HasSingleton<SeasonState>() ? SystemAPI.GetSingleton<SeasonState>() : default;
            HudModel.Plates = SystemAPI.HasSingleton<PlateCollection>() ? SystemAPI.GetSingleton<PlateCollection>() : default;
            HudModel.Skills = SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>() : default;
            HudModel.Stars = SystemAPI.HasSingleton<StationStars>() ? SystemAPI.GetSingleton<StationStars>() : default;
            HudModel.Road = SystemAPI.HasSingleton<RoadEvent>() ? SystemAPI.GetSingleton<RoadEvent>() : default;
            HudModel.Hosted = SystemAPI.HasSingleton<HostedEvents>() ? SystemAPI.GetSingleton<HostedEvents>() : default;

            DrainEvents();
            CopyProps();
            CopyShop();
            CopyServices();
            CopyWash();
            CopyFacilities();
            CopyStaff();
            CopyRenovations();
            CopyQuestTarget();
            CopyCards();
            CopyHistory();
            HudModel.Stats = SystemAPI.HasSingleton<StationStats>() ? SystemAPI.GetSingleton<StationStats>() : default;
            HudModel.Achievements = SystemAPI.HasSingleton<Achievements>() ? SystemAPI.GetSingleton<Achievements>() : default;
            CopyFuel();
            CopyCars();
            CopyPumps();
            CopyHint();
        }

        private void DrainEvents()
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            float weeklyBills = 0f;
            for (int i = 0; i < events.Length; i++)
            {
                var stationEvent = events[i];
                HudModel.Events.Add(stationEvent);

                string fuel = GameTexts.FuelName(stationEvent.Fuel);
                switch (stationEvent.Type)
                {
                    case StationEventType.CustomerLeftAngry:
                        HudModel.Notify(Loc.T("msg.customerAngry"));
                        break;
                    case StationEventType.FuelRanOut:
                        HudModel.Notify(Loc.F("msg.fuelRanOut", fuel));
                        break;
                    case StationEventType.FuelDelivered:
                        HudModel.Notify(Loc.F("msg.fuelDelivered", stationEvent.Value, fuel));
                        break;
                    case StationEventType.PumpBroken:
                        HudModel.Notify(Loc.F("msg.pumpBroken", stationEvent.Value));
                        break;
                    case StationEventType.PumpRepaired:
                        HudModel.Notify(Loc.F("msg.pumpRepaired", stationEvent.Value));
                        break;
                    case StationEventType.NotEnoughMoney:
                        HudModel.Notify(Loc.F("msg.noMoney", stationEvent.Value));
                        break;
                    case StationEventType.LevelUp:
                        HudModel.Notify(Loc.F("msg.levelUp", stationEvent.Value));
                        break;
                    case StationEventType.TipReceived:
                        HudModel.Notify(Loc.F("msg.tip", stationEvent.Value));
                        break;
                    case StationEventType.FuelStolen:
                        HudModel.Notify(Loc.F("msg.fuelStolen", stationEvent.Value));
                        break;
                    case StationEventType.ThiefCaught:
                        HudModel.Notify(Loc.T("msg.thiefCaught"));
                        break;
                    case StationEventType.MarketChanged:
                        HudModel.Notify(Loc.F("msg.market", stationEvent.Value));
                        break;
                    case StationEventType.Vandals:
                        HudModel.Notify(Loc.T("msg.vandals"));
                        break;
                    case StationEventType.RushHourStarted:
                        HudModel.Notify(Loc.T("msg.rushHour"));
                        break;
                    case StationEventType.SandstormStarted:
                        HudModel.Notify(Loc.T("msg.sandstorm"));
                        break;
                    case StationEventType.InspectionPassed:
                        HudModel.Notify(Loc.F("msg.inspectionPassed", stationEvent.Value));
                        break;
                    case StationEventType.ShopEmpty:
                        HudModel.Notify(Loc.T("msg.shopEmpty"));
                        break;
                    case StationEventType.RestroomDisgusting:
                        HudModel.Notify(Loc.T("msg.restroomDisgusting"));
                        break;
                    case StationEventType.WorkerStole:
                        HudModel.Notify(Loc.F("msg.workerStole", stationEvent.Value));
                        break;
                    case StationEventType.AchievementUnlocked:
                        HudModel.Notify(Loc.F("msg.achievement", Loc.T($"achievement.{(AchievementId)(int)stationEvent.Value}.name")));
                        break;
                    case StationEventType.CustomerReview:
                        HudModel.Reviews.Insert(0, new Review
                        {
                            Stars = (int)stationEvent.Value,
                            Variant = UnityEngine.Random.Range(0, ReviewMath.VariantsPerStar),
                            Day = HudModel.Day,
                            Hour = HudModel.Hour
                        });
                        if (HudModel.Reviews.Count > HudModel.MaxReviews)
                            HudModel.Reviews.RemoveAt(HudModel.Reviews.Count - 1);
                        break;
                    case StationEventType.TruckArrived:
                        HudModel.Notify(Loc.T(stationEvent.Value > 0.5f ? "msg.tankerArrived" : "msg.cargoArrived"));
                        break;
                    case StationEventType.RenovationDone:
                        HudModel.Notify(Loc.F("msg.renovated", Loc.T($"renovation.{(RenovationKind)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.RenovationNeedsLevel:
                        HudModel.Notify(Loc.F("msg.renovationNeedsLevel", stationEvent.Value));
                        break;
                    case StationEventType.MotelPaid:
                        HudModel.Notify(Loc.F("msg.motelPaid", stationEvent.Value));
                        break;
                    case StationEventType.TruckParked:
                        HudModel.Notify(Loc.T("msg.truckParked"));
                        break;
                    case StationEventType.ParkingPaid:
                        HudModel.Notify(Loc.F("msg.parkingPaid", stationEvent.Value));
                        break;
                    case StationEventType.ProductsDelivered:
                        HudModel.Notify(Loc.F("msg.productsDelivered", stationEvent.Value));
                        break;
                    case StationEventType.InspectionFailed:
                        HudModel.Notify(Loc.F("msg.inspectionFailed", stationEvent.Value));
                        break;
                    case StationEventType.UtilitiesPaid:
                        HudModel.LastUtilities = stationEvent.Value;
                        break;
                    case StationEventType.TaxPaid:
                    case StationEventType.LoanPayment:
                    case StationEventType.InsurancePremiumPaid:
                        weeklyBills += stationEvent.Value;
                        break;
                    case StationEventType.LoanRepaid:
                        if (stationEvent.Value <= 0f)
                            HudModel.Notify(Loc.T("msg.loanPaidOff"));
                        break;
                    case StationEventType.InsurancePayout:
                        HudModel.Notify(Loc.F("msg.insurancePayout", stationEvent.Value));
                        break;
                    case StationEventType.BankruptcyWarning:
                        HudModel.Notify(Loc.F("msg.bankruptcyWarning", stationEvent.Value));
                        break;
                    case StationEventType.CompetitorOpened:
                        HudModel.Notify(Loc.T("msg.competitorOpened"));
                        break;
                    case StationEventType.CompetitorPriceCut:
                        HudModel.Notify(Loc.T("msg.competitorPriceCut"));
                        break;
                    case StationEventType.CompetitorPriceRise:
                        HudModel.Notify(Loc.T("msg.competitorPriceRise"));
                        break;
                    case StationEventType.CompetitorPromoStarted:
                        HudModel.Notify(Loc.T($"msg.competitorPromo.{(CompetitorPromo)(int)stationEvent.Value}"));
                        break;
                    case StationEventType.RegularArrived:
                        HudModel.Notify(stationEvent.Value > 0.5f
                            ? Loc.F("msg.regularMet", GameTexts.RegularName(stationEvent.Subject), GameTexts.RegularAbout(stationEvent.Subject))
                            : Loc.F("msg.regularArrived", GameTexts.RegularName(stationEvent.Subject),
                                Loc.T($"regular.{stationEvent.Subject - 1}.line.{UnityEngine.Random.Range(0, 2)}")));
                        break;
                    case StationEventType.RegularVisit:
                        // The review pushed just before belongs to this regular.
                        if (HudModel.Reviews.Count > 0)
                        {
                            var review = HudModel.Reviews[0];
                            review.RegularId = stationEvent.Subject;
                            HudModel.Reviews[0] = review;
                        }
                        break;
                    case StationEventType.RegularLost:
                        HudModel.Notify(Loc.F(HudModel.Competitor.Active && !HudModel.Competitor.BoughtOut ? "msg.regularLost" : "msg.regularLostNoRival",
                            GameTexts.RegularName(stationEvent.Subject)));
                        break;
                    case StationEventType.RegularBestFriend:
                        HudModel.Notify(Loc.F("msg.regularFriend", GameTexts.RegularName(stationEvent.Subject)));
                        break;
                    case StationEventType.CriticArticle:
                        HudModel.Notify(Loc.T(stationEvent.Value > 1f ? "msg.criticPraise" : "msg.criticPan"));
                        break;
                    case StationEventType.SpecialArrived:
                        HudModel.Notify(Loc.F($"msg.special.{(CustomerType)(int)stationEvent.Value}", stationEvent.Subject));
                        break;
                    case StationEventType.EmergencyServed:
                        HudModel.Notify(Loc.T("msg.emergencyServed"));
                        break;
                    case StationEventType.ContractOffered:
                        HudModel.Notify(Loc.F("msg.contractOffer", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.ContractAccepted:
                        HudModel.Notify(Loc.F("msg.contractAccepted", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.ContractPenalty:
                        HudModel.Notify(Loc.F("msg.contractPenalty", stationEvent.Value));
                        break;
                    case StationEventType.ContractCompleted:
                        HudModel.Notify(Loc.F("msg.contractCompleted", stationEvent.Value));
                        break;
                    case StationEventType.ContractCancelled:
                        HudModel.Notify(Loc.F("msg.contractCancelled", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.WorkerQuit:
                        HudModel.Notify(Loc.F("msg.workerQuit", GameTexts.RoleName((StaffRole)(int)stationEvent.Value),
                            GameTexts.StaffName(stationEvent.Subject)));
                        break;
                    case StationEventType.ProductsSpoiled:
                        HudModel.Notify(Loc.F("msg.spoiled", stationEvent.Value, GameTexts.ProductName((ProductType)stationEvent.Subject)));
                        break;
                    case StationEventType.ProductsShort:
                        HudModel.Notify(Loc.F("msg.short", stationEvent.Value, GameTexts.ProductName((ProductType)stationEvent.Subject)));
                        break;
                    case StationEventType.SuspiciousCustomer:
                        HudModel.Notify(Loc.T("msg.suspicious"));
                        break;
                    case StationEventType.ShoplifterCaught:
                        HudModel.Notify(Loc.T("msg.shoplifterCaught"));
                        break;
                    case StationEventType.GoodsStolen:
                        HudModel.Notify(Loc.F("msg.goodsStolen", stationEvent.Value));
                        break;
                    case StationEventType.Robbery:
                        HudModel.Notify(Loc.F("msg.robbery", stationEvent.Value));
                        break;
                    case StationEventType.RobberyPrevented:
                        HudModel.Notify(Loc.T("msg.robberyPrevented"));
                        break;
                    case StationEventType.InspectionExpiredGoods:
                        HudModel.Notify(Loc.F("msg.inspectionExpired", stationEvent.Value));
                        break;
                    case StationEventType.DinerOutOfIngredients:
                        HudModel.Notify(Loc.T("msg.dinerEmpty"));
                        break;
                    case StationEventType.FoodWasted:
                        HudModel.Notify(Loc.F("msg.foodWasted", stationEvent.Value, Loc.T($"dish.{(DinerDish)stationEvent.Subject}")));
                        break;
                    case StationEventType.DishCooked:
                        HudModel.Notify(Loc.F("msg.dishCooked", Loc.T($"dish.{(DinerDish)stationEvent.Subject}")));
                        break;
                    case StationEventType.SeasonChanged:
                        HudModel.Notify(Loc.T($"season.{(SeasonKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.WeatherChanged:
                        HudModel.Notify(Loc.T($"weather.{(WeatherKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.NewPlate:
                        HudModel.Notify(Loc.F("msg.newPlate", Loc.T($"plate.{stationEvent.Subject}"), stationEvent.Value, PlateMath.Count));
                        break;
                    case StationEventType.StarGained:
                        HudModel.Notify(Loc.F("msg.starGained", StarText((int)stationEvent.Value)));
                        break;
                    case StationEventType.StarLost:
                        HudModel.Notify(Loc.F("msg.starLost", StarText((int)stationEvent.Value)));
                        break;
                    case StationEventType.RoadEventStarted:
                        HudModel.Notify(Loc.T($"road.{(RoadEventKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.RoadEventEnded:
                        HudModel.Notify(Loc.T($"road.{(RoadEventKind)(int)stationEvent.Value}.end"));
                        break;
                    case StationEventType.HostedEventStarted:
                        HudModel.Notify(Loc.F("msg.eventStarted", Loc.T($"hosted.{(HostedEventKind)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.HostedEventEnded:
                        HudModel.Notify(Loc.F("msg.eventEnded", stationEvent.Value, stationEvent.Subject));
                        break;
                    case StationEventType.SkillLearned:
                        HudModel.Notify(Loc.F("msg.skillLearned", Loc.T($"skill.{(OwnerSkill)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.PropPlaced:
                        HudModel.Notify(Loc.F("msg.propPlaced", GameTexts.PropName((PropType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.CompetitorBoughtOut:
                        HudModel.Notify(Loc.T("msg.competitorBoughtOut"));
                        break;
                }
            }

            if (weeklyBills > 0f)
                HudModel.Notify(Loc.F("msg.weeklyBills", weeklyBills));

            events.Clear();
        }

        private void CopyProps()
        {
            HudModel.Props.Clear();
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
                HudModel.Props.Add(prop.ValueRO);

            HudModel.HasBuildArea = SystemAPI.HasSingleton<BuildArea>();
            HudModel.NoBuildZones.Clear();
            if (!HudModel.HasBuildArea)
                return;

            var areaEntity = SystemAPI.GetSingletonEntity<BuildArea>();
            HudModel.BuildArea = SystemAPI.GetComponent<BuildArea>(areaEntity);
            foreach (var zone in SystemAPI.GetBuffer<NoBuildZone>(areaEntity, true))
                HudModel.NoBuildZones.Add(zone);
            if (SystemAPI.HasSingleton<PropEffects>())
                HudModel.PropEffects = SystemAPI.GetSingleton<PropEffects>();
        }

        private static string StarText(int stars) =>
            new string('★', UnityEngine.Mathf.Clamp(stars, 0, 5)) + new string('☆', 5 - UnityEngine.Mathf.Clamp(stars, 0, 5));

        private void CopyServices()
        {
            HudModel.HasDiner = SystemAPI.HasSingleton<Diner>() && HudModel.Upgrades.Diner > 0;
            if (SystemAPI.HasSingleton<Diner>())
            {
                var dinerEntity = SystemAPI.GetSingletonEntity<Diner>();
                HudModel.Diner = SystemAPI.GetComponent<Diner>(dinerEntity);
                var counter = SystemAPI.GetBuffer<DinerCounter>(dinerEntity, true);
                for (int i = 0; i < DinerDishes.Count; i++)
                    HudModel.DinerCounter[i] = i < counter.Length ? counter[i] : default;
            }

            HudModel.ChargersOpen = 0;
            HudModel.ChargersUsed = 0;
            if (!SystemAPI.HasSingleton<ChargingStation>())
                return;

            var spots = SystemAPI.GetBuffer<ChargerSpot>(SystemAPI.GetSingletonEntity<ChargingStation>(), true);
            HudModel.ChargersOpen = EvMath.OpenChargers(HudModel.Upgrades.EvCharger, spots.Length);
            for (int i = 0; i < HudModel.ChargersOpen; i++)
            {
                if (spots[i].Occupant != Entity.Null)
                    HudModel.ChargersUsed++;
            }
        }

        private void CopyShop()
        {
            HudModel.HasShop = SystemAPI.HasSingleton<Shop>();
            for (int i = 0; i < ProductTypes.Count; i++)
                HudModel.PendingProducts[i] = 0;

            if (!HudModel.HasShop)
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            for (int i = 0; i < ProductTypes.Count; i++)
                HudModel.Products[i] = i < shelves.Length ? shelves[i] : default;
            HudModel.Supplier = SystemAPI.GetSingleton<Shop>().Supplier;

            foreach (var delivery in SystemAPI.Query<RefRO<ProductDelivery>>())
                HudModel.PendingProducts[(int)delivery.ValueRO.Type] += delivery.ValueRO.Count;

            int inShop = 0;
            foreach (var pedestrian in SystemAPI.Query<RefRO<Pedestrian>>())
            {
                if (pedestrian.ValueRO.State == PedestrianState.InShop)
                    inShop++;
            }

            foreach (var car in SystemAPI.Query<RefRO<Car>>())
            {
                // Without a pedestrian model the driver is "inside" for the whole trip.
                if (car.ValueRO.State == CarState.Shopping && car.ValueRO.DriverAway && car.ValueRO.Timer > 0f)
                    inShop++;
            }

            HudModel.PedestriansInShop = inShop;
        }

        private void CopyWash()
        {
            HudModel.HasWash = SystemAPI.HasSingleton<CarWash>();
            HudModel.WashBusy = false;
            HudModel.WashTimeLeft = 0f;
            if (!HudModel.HasWash)
                return;

            var occupant = SystemAPI.GetSingleton<CarWash>().Occupant;
            if (occupant == Entity.Null || !SystemAPI.Exists(occupant) || !SystemAPI.HasComponent<Car>(occupant))
                return;

            var car = SystemAPI.GetComponent<Car>(occupant);
            HudModel.WashBusy = true;
            HudModel.WashTimeLeft = car.State == CarState.Washing ? car.Timer : 0f;
        }

        private void CopyFacilities()
        {
            HudModel.HasParking = SystemAPI.HasSingleton<TruckParking>();
            if (HudModel.HasParking)
            {
                var spots = SystemAPI.GetBuffer<ParkingSpot>(SystemAPI.GetSingletonEntity<TruckParking>());
                HudModel.ParkingTotal = spots.Length;
                HudModel.ParkingOpen = FacilityMath.OpenSpots(HudModel.Upgrades.TruckParking, spots.Length);
                int used = 0;
                for (int i = 0; i < spots.Length; i++)
                {
                    if (spots[i].Occupant != Entity.Null && SystemAPI.Exists(spots[i].Occupant))
                        used++;
                }

                HudModel.ParkingUsed = used;
            }

            HudModel.HasTireService = SystemAPI.HasSingleton<TireService>();
            HudModel.TireCarWaiting = false;
            HudModel.TireTimeLeft = 0f;
            if (HudModel.HasTireService)
            {
                var occupant = SystemAPI.GetSingleton<TireService>().Occupant;
                if (occupant != Entity.Null && SystemAPI.Exists(occupant) && SystemAPI.HasComponent<Car>(occupant))
                {
                    var car = SystemAPI.GetComponent<Car>(occupant);
                    HudModel.TireCarWaiting = car.State == CarState.WaitingForTires;
                    HudModel.TireTimeLeft = car.State == CarState.ChangingTires ? car.Timer : 0f;
                }
            }

            HudModel.PaintScheme = SystemAPI.HasSingleton<StationStyle>() ? SystemAPI.GetSingleton<StationStyle>().Scheme : 1;

            HudModel.HasMotel = SystemAPI.HasSingleton<Motel>();
            HudModel.MotelUsed = 0;
            HudModel.MotelDirty = 0;
            if (HudModel.HasMotel)
            {
                var rooms = SystemAPI.GetBuffer<MotelRoom>(SystemAPI.GetSingletonEntity<Motel>());
                HudModel.MotelOpen = FacilityMath.OpenRooms(HudModel.Upgrades.Motel, rooms.Length);
                for (int i = 0; i < rooms.Length; i++)
                {
                    if (rooms[i].Occupant != Entity.Null && SystemAPI.Exists(rooms[i].Occupant))
                        HudModel.MotelUsed++;
                    if (rooms[i].Dirty)
                        HudModel.MotelDirty++;
                }
            }

            HudModel.HasRestroom = SystemAPI.HasSingleton<Restroom>();
            HudModel.RestroomDirt = HudModel.HasRestroom ? SystemAPI.GetSingleton<Restroom>().Dirt : 0f;
        }

        /// <summary>Picks the place the current quest is about, nearest to the player.</summary>
        private void CopyQuestTarget()
        {
            HudModel.HasQuestTarget = false;
            float3 player = float3.zero;
            bool hasPlayer = false;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                player = transform.ValueRO.Position;
                hasPlayer = true;
            }

            if (!hasPlayer)
                return;

            float best = float.MaxValue;
            float3 target = float3.zero;

            switch (HudModel.Quest.Goal)
            {
                case QuestGoal.CollectTrash:
                    foreach (var transform in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<Trash>())
                        Consider(ref best, ref target, player, transform.ValueRO.Position);
                    break;

                case QuestGoal.RepairPump:
                    foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                    {
                        if (pump.ValueRO.Condition < ProgressMath.RepairThreshold &&
                            pump.ValueRO.RequiredUpgradeLevel <= HudModel.Upgrades.ExtraPump)
                            Consider(ref best, ref target, player, pump.ValueRO.InteractionPoint);
                    }
                    break;

                case QuestGoal.ServeCustomers:
                case QuestGoal.CatchThief:
                    foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                    {
                        var occupant = pump.ValueRO.Occupant;
                        if (occupant != Entity.Null && SystemAPI.Exists(occupant) && SystemAPI.HasComponent<Car>(occupant) &&
                            SystemAPI.GetComponent<Car>(occupant).State == CarState.WaitingForService)
                            Consider(ref best, ref target, player, pump.ValueRO.InteractionPoint);
                    }
                    break;

                case QuestGoal.Renovate:
                    foreach (var renovation in SystemAPI.Query<RefRO<Renovation>>())
                    {
                        if (!renovation.ValueRO.Done)
                            Consider(ref best, ref target, player, renovation.ValueRO.Position);
                    }
                    break;

                case QuestGoal.CleanRestroom:
                    if (SystemAPI.HasSingleton<Restroom>() && SystemAPI.GetSingleton<Restroom>().Dirt > FacilityMath.RestroomCleanThreshold)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<Restroom>().Door);
                    break;

                case QuestGoal.ChangeTires:
                    if (HudModel.TireCarWaiting)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<TireService>().Bay);
                    break;

                case QuestGoal.HostGuests:
                    if (HudModel.MotelDirty > 0)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<Motel>().Door);
                    break;
            }

            if (best == float.MaxValue)
                return;

            HudModel.HasQuestTarget = true;
            HudModel.QuestTarget = target;
        }

        private static void Consider(ref float best, ref float3 target, float3 player, float3 position)
        {
            float distance = math.distancesq(position.xz, player.xz);
            if (distance < best)
            {
                best = distance;
                target = position;
            }
        }

        private const int MaxCards = 12;

        /// <summary>Cards for customers who are waiting or being served, nearest to the pumps first.</summary>
        private void CopyCards()
        {
            HudModel.Cards.Clear();
            foreach (var (car, patience, transform) in SystemAPI.Query<RefRO<Car>, RefRO<Patience>, RefRO<LocalTransform>>())
            {
                var state = car.ValueRO.State;
                bool show = state is CarState.Queued or CarState.DrivingToPump or CarState.WaitingForService
                    or CarState.Fueling or CarState.WaitingForTires;
                if (!show || HudModel.Cards.Count >= MaxCards)
                    continue;

                HudModel.Cards.Add(new CarCard
                {
                    Position = transform.ValueRO.Position,
                    Customer = car.ValueRO.Customer,
                    State = state,
                    Fuel = car.ValueRO.FuelType,
                    RequestedLiters = car.ValueRO.RequestedLiters,
                    ReceivedLiters = car.ValueRO.ReceivedLiters,
                    PatienceRatio = patience.ValueRO.Max > 0f ? patience.ValueRO.Current / patience.ValueRO.Max : 0f,
                    RegularId = car.ValueRO.RegularId,
                    ContractId = car.ValueRO.ContractId
                });
            }
        }

        private void CopyHistory()
        {
            HudModel.History.Clear();
            if (!SystemAPI.HasSingleton<DayHistoryEntry>())
                return;

            var history = SystemAPI.GetSingletonBuffer<DayHistoryEntry>(true);
            for (int i = 0; i < history.Length; i++)
                HudModel.History.Add(history[i]);
        }

        private void CopyRenovations()
        {
            ulong done = 0;
            int total = 0;
            foreach (var renovation in SystemAPI.Query<RefRO<Renovation>>())
            {
                total++;
                if (renovation.ValueRO.Done && renovation.ValueRO.Id is >= 0 and < 64)
                    done |= 1UL << renovation.ValueRO.Id;
            }

            HudModel.RenovationsDone = done;
            HudModel.RenovationsTotal = total;
        }

        private void CopyStaff()
        {
            HudModel.Workers.Clear();
            HudModel.Candidates.Clear();
            HudModel.Staff = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>() : default;

            foreach (var worker in SystemAPI.Query<RefRO<Worker>>())
                HudModel.Workers.Add(worker.ValueRO);
            HudModel.Workers.Sort((a, b) => a.Id.CompareTo(b.Id));

            if (!SystemAPI.HasSingleton<StaffRoster>())
                return;

            var candidates = SystemAPI.GetBuffer<StaffCandidate>(SystemAPI.GetSingletonEntity<StaffRoster>());
            for (int i = 0; i < candidates.Length; i++)
                HudModel.Candidates.Add(candidates[i]);
        }

        private void CopyFuel()
        {
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                HudModel.Fuel[i] = i < stock.Length ? stock[i] : default;
                HudModel.PendingDelivery[i] = 0f;
            }

            foreach (var delivery in SystemAPI.Query<RefRO<FuelDelivery>>())
                HudModel.PendingDelivery[(int)delivery.ValueRO.Type] += delivery.ValueRO.Liters;
        }

        private void CopyCars()
        {
            int queueLength = 0;
            int carsOnSite = 0;
            bool anyFueling = false;
            foreach (var car in SystemAPI.Query<RefRO<Car>>())
            {
                var state = car.ValueRO.State;
                if (state == CarState.Queued || state == CarState.Arriving)
                    queueLength++;
                if (state != CarState.Leaving)
                    carsOnSite++;
                anyFueling |= state == CarState.Fueling;
            }

            HudModel.QueueLength = queueLength;
            HudModel.CarsOnSite = carsOnSite;
            HudModel.AnyFueling = anyFueling;
        }

        private void CopyPumps()
        {
            HudModel.Pumps.Clear();
            foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                HudModel.Pumps.Add(Describe(pump.ValueRO));
            HudModel.Pumps.Sort((a, b) => a.Number.CompareTo(b.Number));
        }

        private void CopyHint()
        {
            HudModel.Hint = InteractionHint.None;
            foreach (var interaction in SystemAPI.Query<RefRO<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                    HudModel.Hint = HintFor(Describe(SystemAPI.GetComponent<Pump>(pumpEntity)));

                if (HudModel.Hint != InteractionHint.CanStartFueling && pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity) &&
                    SystemAPI.GetComponent<Pump>(pumpEntity).Condition < ProgressMath.RepairThreshold)
                    HudModel.Hint = InteractionHint.Repair;

                bool fuelingAction = HudModel.Hint is InteractionHint.CanStartFueling or InteractionHint.Repair;
                if (!fuelingAction && NearWaitingTireCar())
                {
                    HudModel.Hint = InteractionHint.Tires;
                    fuelingAction = true;
                }

                if (!fuelingAction && NearDirtyMotel())
                {
                    HudModel.Hint = InteractionHint.MotelRoom;
                    fuelingAction = true;
                }

                if (!fuelingAction && NearDirtyRestroom())
                {
                    HudModel.Hint = InteractionHint.Restroom;
                    fuelingAction = true;
                }

                // Same order as the systems that consume the interact press.
                var renovationEntity = interaction.ValueRO.NearbyRenovation;
                if (!fuelingAction && renovationEntity != Entity.Null && SystemAPI.Exists(renovationEntity))
                {
                    HudModel.NearRenovationKind = SystemAPI.GetComponent<Renovation>(renovationEntity).Kind;
                    HudModel.Hint = InteractionHint.Renovate;
                    fuelingAction = true;
                }

                var trash = interaction.ValueRO.NearbyTrash;
                if (!fuelingAction && trash != Entity.Null && SystemAPI.Exists(trash))
                    HudModel.Hint = InteractionHint.Trash;
            }

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
                HudModel.PlayerPosition = transform.ValueRO.Position;

            // The grill comes before the laptop: E cooks there.
            if (HudModel.Hint is InteractionHint.None or InteractionHint.PumpFree && HudModel.Upgrades.Diner > 0 &&
                SystemAPI.HasSingleton<Diner>())
            {
                var grillOffset = HudModel.PlayerPosition - (UnityEngine.Vector3)SystemAPI.GetSingleton<Diner>().Grill;
                grillOffset.y = 0f;
                if (grillOffset.sqrMagnitude <= 2.5f * 2.5f)
                    HudModel.Hint = InteractionHint.Grill;
            }

            HudModel.HasLaptop = SystemAPI.HasSingleton<Laptop>();
            if (!HudModel.HasLaptop)
                return;

            HudModel.LaptopPosition = SystemAPI.GetSingleton<Laptop>().Position;
            // The laptop has the lowest priority: E does the station work first.
            var offset = HudModel.PlayerPosition - HudModel.LaptopPosition;
            offset.y = 0f;
            if (HudModel.Hint is InteractionHint.None or InteractionHint.PumpFree &&
                offset.sqrMagnitude <= HudModel.LaptopRadius * HudModel.LaptopRadius)
                HudModel.Hint = InteractionHint.Laptop;
        }

        private bool NearWaitingTireCar()
        {
            if (!HudModel.TireCarWaiting || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var bay = SystemAPI.GetSingleton<TireService>().Bay;
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, bay.xz) <= radius * radius)
                    return true;
            }

            return false;
        }

        private bool NearDirtyMotel()
        {
            if (HudModel.MotelDirty == 0 || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var door = SystemAPI.GetSingleton<Motel>().Door;
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, door.xz) <= radius * radius)
                    return true;
            }

            return false;
        }

        private bool NearDirtyRestroom()
        {
            if (!SystemAPI.HasSingleton<Restroom>() || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var restroom = SystemAPI.GetSingleton<Restroom>();
            if (restroom.Dirt <= FacilityMath.RestroomCleanThreshold)
                return false;

            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, restroom.Door.xz) <= radius * radius)
                    return true;
            }

            return false;
        }

        private PumpInfo Describe(Pump pump)
        {
            var info = new PumpInfo
            {
                Number = pump.Number,
                Locked = pump.RequiredUpgradeLevel > HudModel.Upgrades.ExtraPump,
                Condition = pump.Condition
            };

            var occupant = pump.Occupant;
            if (occupant == Entity.Null || !SystemAPI.Exists(occupant) || !SystemAPI.HasComponent<Car>(occupant))
                return info;

            var car = SystemAPI.GetComponent<Car>(occupant);
            var patience = SystemAPI.GetComponent<Patience>(occupant);
            info.Occupied = true;
            info.Customer = car.Customer;
            info.CarState = car.State;
            info.FuelType = car.FuelType;
            info.RequestedLiters = car.RequestedLiters;
            info.ReceivedLiters = car.ReceivedLiters;
            info.PatienceRatio = patience.Max > 0f ? patience.Current / patience.Max : 0f;
            return info;
        }

        private static InteractionHint HintFor(PumpInfo pump)
        {
            if (!pump.Occupied)
                return InteractionHint.PumpFree;

            return pump.CarState switch
            {
                CarState.WaitingForService => InteractionHint.CanStartFueling,
                CarState.Fueling => InteractionHint.Fueling,
                _ => InteractionHint.CarArriving
            };
        }
    }
}
