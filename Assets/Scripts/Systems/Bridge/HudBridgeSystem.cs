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

            DrainEvents();
            CopyShop();
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
                    case StationEventType.CompetitorBoughtOut:
                        HudModel.Notify(Loc.T("msg.competitorBoughtOut"));
                        break;
                }
            }

            if (weeklyBills > 0f)
                HudModel.Notify(Loc.F("msg.weeklyBills", weeklyBills));

            events.Clear();
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
                    PatienceRatio = patience.ValueRO.Max > 0f ? patience.ValueRO.Current / patience.ValueRO.Max : 0f
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
