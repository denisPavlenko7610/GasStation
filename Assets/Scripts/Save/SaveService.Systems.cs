using GasStation.Components;
using GasStation.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GasStation.Save
{
    /// <summary>Capture and restore of the systems added after the core game: shop extras, visitors, contracts, progress, diner, props.</summary>
    public static partial class SaveService
    {
        private static void CaptureShop(EntityManager entityManager, SaveData data)
        {
            using var shopQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Shop>());
            if (shopQuery.CalculateEntityCount() != 1)
                return;

            var pending = new int[ProductTypes.Count];
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductDelivery>()))
            using (var deliveries = query.ToComponentDataArray<ProductDelivery>(Allocator.Temp))
            {
                foreach (var delivery in deliveries)
                    pending[(int)delivery.Type] += delivery.Count;
            }

            var shelves = entityManager.GetBuffer<ShopProduct>(shopQuery.GetSingletonEntity(), true);
            data.products = new ProductSaveData[shelves.Length];
            for (int i = 0; i < shelves.Length; i++)
            {
                // Paid orders are saved as delivered.
                int extra = i < pending.Length ? pending[i] : 0;
                data.products[i] = new ProductSaveData
                {
                    stock = Mathf.Min(shelves[i].Capacity, shelves[i].Stock + extra),
                    capacity = shelves[i].Capacity,
                    sellPrice = shelves[i].SellPrice,
                    age = shelves[i].Age,
                    promo = shelves[i].Promo
                };
            }

            data.supplier = (int)entityManager.GetComponentData<Shop>(shopQuery.GetSingletonEntity()).Supplier;
        }

        private static void RestoreSupplier(EntityManager entityManager, int supplier)
        {
            using var shopQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Shop>());
            if (shopQuery.CalculateEntityCount() != 1)
                return;

            var entity = shopQuery.GetSingletonEntity();
            var shop = entityManager.GetComponentData<Shop>(entity);
            shop.Supplier = (SupplierKind)Mathf.Clamp(supplier, 0, 1);
            entityManager.SetComponentData(entity, shop);
        }

        private static void RestoreShop(EntityManager entityManager, ProductSaveData[] saved)
        {
            using var shopQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Shop>());
            if (shopQuery.CalculateEntityCount() != 1)
                return;

            var shelves = entityManager.GetBuffer<ShopProduct>(shopQuery.GetSingletonEntity());
            for (int i = 0; i < shelves.Length && i < saved.Length; i++)
            {
                if (saved[i] == null)
                    continue;

                var shelf = shelves[i];
                shelf.Capacity = saved[i].capacity > 0 ? saved[i].capacity : shelf.Capacity;
                shelf.Stock = Mathf.Clamp(saved[i].stock, 0, shelf.Capacity);
                shelf.SellPrice = saved[i].sellPrice > 0f ? saved[i].sellPrice : shelf.SellPrice;
                shelf.Age = Mathf.Max(0f, saved[i].age);
                shelf.Promo = saved[i].promo;
                shelves[i] = shelf;
            }
        }

        private static void CaptureVisitors(EntityManager entityManager, Entity station, SaveData data)
        {
            if (entityManager.HasBuffer<RegularState>(station))
            {
                var regulars = entityManager.GetBuffer<RegularState>(station, true);
                data.regulars = new RegularSaveData[regulars.Length];
                for (int i = 0; i < regulars.Length; i++)
                    data.regulars[i] = RegularSaveData.From(regulars[i]);
            }

            if (entityManager.HasComponent<Buzz>(station))
            {
                var buzz = entityManager.GetComponentData<Buzz>(station);
                data.buzzFactor = buzz.Factor;
                data.buzzDays = buzz.DaysLeft;
            }

            if (entityManager.HasComponent<Visitors>(station))
            {
                var visitors = entityManager.GetComponentData<Visitors>(station);
                data.lastCriticDay = visitors.LastCriticDay;
                data.lastBusDay = visitors.LastBusDay;
            }
        }

        /// <summary>Older saves meet every regular again from scratch.</summary>
        private static void RestoreVisitors(EntityManager entityManager, Entity station, SaveData data)
        {
            if (entityManager.HasBuffer<RegularState>(station))
            {
                var regulars = entityManager.GetBuffer<RegularState>(station);
                for (int i = 0; i < regulars.Length; i++)
                {
                    var saved = data.regulars != null && i < data.regulars.Length ? data.regulars[i] : null;
                    regulars[i] = saved != null
                        ? saved.ToState()
                        : new RegularState { Loyalty = VisitorMath.StartLoyalty, LastVisitDay = -1 };
                }
            }

            if (entityManager.HasComponent<Buzz>(station))
            {
                bool running = data.buzzFactor > 0f && data.buzzDays > 0;
                entityManager.SetComponentData(station, new Buzz
                {
                    Factor = running ? data.buzzFactor : 1f,
                    DaysLeft = running ? data.buzzDays : 0
                });
            }

            if (entityManager.HasComponent<Visitors>(station))
            {
                var visitors = entityManager.GetComponentData<Visitors>(station);
                bool hasDays = data.version >= 15;
                visitors.LastCriticDay = hasDays ? data.lastCriticDay : -100;
                visitors.LastBusDay = hasDays ? data.lastBusDay : -100;
                visitors.LastRolledHour = -1;
                entityManager.SetComponentData(station, visitors);
            }

            // Guests invited before the load belong to the old game.
            using var spawnerQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<SpawnRequest>());
            using var spawners = spawnerQuery.ToEntityArray(Allocator.Temp);
            foreach (var spawner in spawners)
                entityManager.GetBuffer<SpawnRequest>(spawner).Clear();
        }

        private static void CaptureContracts(EntityManager entityManager, Entity station, SaveData data)
        {
            if (entityManager.HasBuffer<ContractOffer>(station))
            {
                var offers = entityManager.GetBuffer<ContractOffer>(station, true);
                data.offers = new ContractSaveData[offers.Length];
                for (int i = 0; i < offers.Length; i++)
                    data.offers[i] = ContractSaveData.From(offers[i]);
            }

            if (entityManager.HasBuffer<Contract>(station))
            {
                var contracts = entityManager.GetBuffer<Contract>(station, true);
                data.contracts = new ContractSaveData[contracts.Length];
                for (int i = 0; i < contracts.Length; i++)
                    data.contracts[i] = ContractSaveData.From(contracts[i]);
            }

            if (entityManager.HasComponent<ContractBoard>(station))
            {
                var board = entityManager.GetComponentData<ContractBoard>(station);
                data.nextContractId = board.NextId;
                data.daysToNextOffer = board.DaysToNextOffer;
            }
        }

        private static void RestoreContracts(EntityManager entityManager, Entity station, SaveData data)
        {
            int maxId = 0;
            if (entityManager.HasBuffer<ContractOffer>(station))
            {
                var offers = entityManager.GetBuffer<ContractOffer>(station);
                offers.Clear();
                if (data.offers != null)
                {
                    foreach (var offer in data.offers)
                    {
                        if (offer == null)
                            continue;
                        offers.Add(offer.ToOffer());
                        maxId = Mathf.Max(maxId, offer.id);
                    }
                }
            }

            if (entityManager.HasBuffer<Contract>(station))
            {
                var time = entityManager.GetComponentData<GameTime>(station);
                var contracts = entityManager.GetBuffer<Contract>(station);
                contracts.Clear();
                if (data.contracts != null)
                {
                    foreach (var contract in data.contracts)
                    {
                        if (contract == null)
                            continue;
                        contracts.Add(contract.ToContract(time.Day, (int)time.Hour));
                        maxId = Mathf.Max(maxId, contract.id);
                    }
                }
            }

            if (entityManager.HasComponent<ContractBoard>(station))
            {
                var board = entityManager.GetComponentData<ContractBoard>(station);
                board.NextId = Mathf.Max(maxId + 1, data.nextContractId, 1);
                board.DaysToNextOffer = data.version >= 16 ? Mathf.Max(1, data.daysToNextOffer) : 1;
                entityManager.SetComponentData(station, board);
            }
        }

        private static void CaptureGrowth(EntityManager entityManager, Entity station, SaveData data)
        {
            if (entityManager.HasComponent<SeasonState>(station))
            {
                var season = entityManager.GetComponentData<SeasonState>(station);
                data.season = (int)season.Season;
                data.weather = (int)season.Weather;
            }

            if (entityManager.HasComponent<PlateCollection>(station))
                data.plates = entityManager.GetComponentData<PlateCollection>(station).Seen;
            data.catName = GasStation.Bridge.StationProfile.CustomCatName;

            if (entityManager.HasComponent<StationRules>(station))
                data.gameMode = (int)entityManager.GetComponentData<StationRules>(station).Mode;
            if (entityManager.HasComponent<Campaign>(station))
            {
                var campaign = entityManager.GetComponentData<Campaign>(station);
                data.campaignActive = campaign.Active;
                data.campaignChapter = campaign.Chapter;
                data.campaignRepaid = campaign.Repaid;
                data.campaignOutcome = (int)campaign.Outcome;
                data.campaignOfferAnswered = campaign.OfferAnswered;
                data.campaignVictoryShown = campaign.VictoryShown;
            }

            if (entityManager.HasComponent<OwnerSkillSet>(station))
                data.skills = entityManager.GetComponentData<OwnerSkillSet>(station).Learned;
            if (entityManager.HasComponent<StationStars>(station))
            {
                var stars = entityManager.GetComponentData<StationStars>(station);
                data.stars = stars.Stars;
                data.bestStars = stars.Best;
            }

            if (entityManager.HasComponent<RoadEvent>(station))
            {
                var road = entityManager.GetComponentData<RoadEvent>(station);
                data.roadEvent = (int)road.Kind;
                data.roadHoursLeft = road.HoursLeft;
            }

            if (entityManager.HasComponent<HostedEvents>(station))
            {
                var hosted = entityManager.GetComponentData<HostedEvents>(station);
                data.plannedEvent = (int)hosted.Planned;
                data.plannedEventDay = hosted.PlannedDay;
                data.lastHostedDay = hosted.LastHostedDay;
            }
        }

        /// <summary>A running hosted event is not saved: it simply ends.</summary>
        private static void RestoreGrowth(EntityManager entityManager, Entity station, SaveData data)
        {
            if (entityManager.HasComponent<SeasonState>(station))
            {
                var season = entityManager.GetComponentData<SeasonState>(station);
                // The calendar decides the season; older saves get the right one for their day.
                season.Season = SeasonMath.SeasonOf(entityManager.GetComponentData<GameTime>(station).Day);
                season.Weather = (WeatherKind)Mathf.Clamp(data.weather, 0, (int)WeatherKind.Snow);
                entityManager.SetComponentData(station, season);
            }

            if (entityManager.HasComponent<PlateCollection>(station))
                entityManager.SetComponentData(station, new PlateCollection { Seen = data.plates });
            GasStation.Bridge.StationProfile.SetCatName(data.catName);

            if (entityManager.HasComponent<Campaign>(station))
                entityManager.SetComponentData(station, new Campaign
                {
                    Active = data.campaignActive,
                    Chapter = Mathf.Clamp(data.campaignChapter, 1, CampaignMath.Chapters),
                    Repaid = Mathf.Max(0f, data.campaignRepaid),
                    Outcome = (CampaignOutcome)Mathf.Clamp(data.campaignOutcome, 0, (int)CampaignOutcome.Sold),
                    OfferAnswered = data.campaignOfferAnswered,
                    VictoryShown = data.campaignVictoryShown
                });

            if (entityManager.HasComponent<OwnerSkillSet>(station))
            {
                var skills = entityManager.GetComponentData<OwnerSkillSet>(station);
                skills.Learned = data.skills;
                entityManager.SetComponentData(station, skills);
            }

            if (entityManager.HasComponent<StationStars>(station))
                entityManager.SetComponentData(station, new StationStars
                {
                    Stars = Mathf.Clamp(data.stars, 0, StarMath.MaxStars),
                    Best = Mathf.Clamp(data.bestStars, 0, StarMath.MaxStars)
                });

            if (entityManager.HasComponent<RoadEvent>(station))
            {
                var road = entityManager.GetComponentData<RoadEvent>(station);
                road.Kind = (RoadEventKind)Mathf.Clamp(data.roadEvent, 0, (int)RoadEventKind.OilCrisis);
                road.HoursLeft = road.Kind == RoadEventKind.None ? 0f : Mathf.Max(0.1f, data.roadHoursLeft);
                road.LastRolledHour = -1;
                entityManager.SetComponentData(station, road);
            }

            if (entityManager.HasComponent<HostedEvents>(station))
                entityManager.SetComponentData(station, new HostedEvents
                {
                    Planned = (HostedEventKind)Mathf.Clamp(data.plannedEvent, 0, HostedEventKinds.Count - 1),
                    PlannedDay = data.plannedEventDay,
                    LastHostedDay = data.lastHostedDay
                });
        }

        private static void CaptureDiner(EntityManager entityManager, SaveData data)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Diner>());
            if (query.CalculateEntityCount() != 1)
                return;

            var entity = query.GetSingletonEntity();
            data.dinerIngredients = entityManager.GetComponentData<Diner>(entity).Ingredients;
            var counter = entityManager.GetBuffer<DinerCounter>(entity, true);
            data.dinerReady = new int[counter.Length];
            data.dinerHoursLeft = new float[counter.Length];
            for (int i = 0; i < counter.Length; i++)
            {
                data.dinerReady[i] = counter[i].Ready;
                data.dinerHoursLeft[i] = counter[i].HoursLeft;
            }
        }

        private static void RestoreDiner(EntityManager entityManager, SaveData data)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Diner>());
            if (query.CalculateEntityCount() != 1 || data.dinerIngredients < 0)
                return;

            var entity = query.GetSingletonEntity();
            var diner = entityManager.GetComponentData<Diner>(entity);
            diner.Ingredients = data.dinerIngredients;
            diner.CookProgress = 0f;
            diner.WarnedEmpty = false;
            entityManager.SetComponentData(entity, diner);

            var counter = entityManager.GetBuffer<DinerCounter>(entity);
            for (int i = 0; i < counter.Length; i++)
            {
                var item = counter[i];
                item.Ready = data.dinerReady != null && i < data.dinerReady.Length ? Mathf.Max(0, data.dinerReady[i]) : 0;
                item.HoursLeft = data.dinerHoursLeft != null && i < data.dinerHoursLeft.Length ? data.dinerHoursLeft[i] : 0f;
                counter[i] = item;
            }
        }

        private static void CaptureProps(EntityManager entityManager, SaveData data)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacedProp>());
            using var props = query.ToComponentDataArray<PlacedProp>(Allocator.Temp);
            data.props = new PropSaveData[props.Length];
            for (int i = 0; i < props.Length; i++)
                data.props[i] = PropSaveData.From(props[i]);
        }

        /// <summary>Replaces every placed prop with the saved ones (none for older saves).</summary>
        private static void RestoreProps(EntityManager entityManager, PropSaveData[] saved)
        {
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacedProp>()))
                entityManager.DestroyEntity(query);

            int nextId = 1;
            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    if (entry == null)
                        continue;
                    var entity = entityManager.CreateEntity();
                    entityManager.AddComponentData(entity, entry.ToProp(nextId++));
                }
            }

            using var areaQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<BuildArea>());
            if (areaQuery.CalculateEntityCount() != 1)
                return;
            var areaEntity = areaQuery.GetSingletonEntity();
            var area = entityManager.GetComponentData<BuildArea>(areaEntity);
            area.NextPropId = nextId;
            entityManager.SetComponentData(areaEntity, area);
        }
    }
}
