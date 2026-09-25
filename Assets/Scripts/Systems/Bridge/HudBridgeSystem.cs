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

            // State first: the messages built from this frame's events read some of it.
            CopyStation();
            CopyFinance();
            CopyProgress();
            CopyVisitors();
            DrainEvents();

            CopyProps();
            CopyShop();
            CopyServices();
            CopyWash();
            CopyFacilities();
            CopyStaff();
            CopyStaffBodies();
            CopyRenovations();
            CopyQuestTarget();
            CopyCards();
            CopyHistory();
            CopyFuel();
            CopyCars();
            CopyPumps();
            CopyHint();
        }

        private void CopyStation()
        {
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
            HudModel.Stats = SystemAPI.HasSingleton<StationStats>() ? SystemAPI.GetSingleton<StationStats>() : default;
            HudModel.Achievements = SystemAPI.HasSingleton<Achievements>() ? SystemAPI.GetSingleton<Achievements>() : default;
        }

        private void CopyFinance()
        {
            if (SystemAPI.HasSingleton<Finance>())
                HudModel.Finance = SystemAPI.GetSingleton<Finance>();
            if (SystemAPI.HasSingleton<StationRules>())
            {
                var rules = SystemAPI.GetSingleton<StationRules>();
                HudModel.Difficulty = rules.Difficulty;
                HudModel.Mode = rules.Mode;
            }

            HudModel.Campaign = SystemAPI.HasSingleton<Campaign>() ? SystemAPI.GetSingleton<Campaign>() : default;
            if (SystemAPI.HasSingleton<Competitor>())
                HudModel.Competitor = SystemAPI.GetSingleton<Competitor>();
            HudModel.GameOver = HudModel.Finance.Bankrupt ||
                                HudModel.Campaign.Outcome is CampaignOutcome.Lost or CampaignOutcome.Sold;
        }

        /// <summary>Long-term progress: season, collections, skills, stars, road and hosted events.</summary>
        private void CopyProgress()
        {
            HudModel.Season = SystemAPI.HasSingleton<SeasonState>() ? SystemAPI.GetSingleton<SeasonState>() : default;
            HudModel.Plates = SystemAPI.HasSingleton<PlateCollection>() ? SystemAPI.GetSingleton<PlateCollection>() : default;
            HudModel.Skills = SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>() : default;
            HudModel.Stars = SystemAPI.HasSingleton<StationStars>() ? SystemAPI.GetSingleton<StationStars>() : default;
            HudModel.Road = SystemAPI.HasSingleton<RoadEvent>() ? SystemAPI.GetSingleton<RoadEvent>() : default;
            HudModel.Hosted = SystemAPI.HasSingleton<HostedEvents>() ? SystemAPI.GetSingleton<HostedEvents>() : default;
        }

        /// <summary>Regulars, the critic's article and the laptop mail.</summary>
        private void CopyVisitors()
        {
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
        }

        private void CopyStaffBodies()
        {
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
    }
}
