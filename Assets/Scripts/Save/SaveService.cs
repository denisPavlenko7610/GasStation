using System;
using System.IO;
using GasStation.Components;
using GasStation.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GasStation.Save
{
    /// <summary>Reads and writes the station state. Must be called from the main thread.</summary>
    public static class SaveService
    {
        private const string FileName = "savegame.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(FilePath);

        /// <summary>State right after baking, used for "new game".</summary>
        public static SaveData Defaults { get; set; }

        public static SaveData Capture(EntityManager entityManager, Entity station)
        {
            var stockBuffer = entityManager.GetBuffer<FuelStock>(station, true);
            var stock = new FuelStock[stockBuffer.Length];
            for (int i = 0; i < stock.Length; i++)
                stock[i] = stockBuffer[i];

            var pending = new float[FuelTypes.Count];
            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FuelDelivery>()))
            using (var deliveries = query.ToComponentDataArray<FuelDelivery>(Allocator.Temp))
            {
                foreach (var delivery in deliveries)
                    pending[(int)delivery.Type] += delivery.Liters;
            }

            var data = SaveData.Create(
                entityManager.GetComponentData<Economy>(station),
                entityManager.GetComponentData<GameTime>(station),
                entityManager.GetComponentData<StationUpgrades>(station),
                stock,
                pending);

            if (entityManager.HasComponent<QuestProgress>(station))
                data.CaptureQuest(entityManager.GetComponentData<QuestProgress>(station));
            if (entityManager.HasComponent<StationLevel>(station))
                data.CaptureLevel(entityManager.GetComponentData<StationLevel>(station));
            if (entityManager.HasComponent<StationStyle>(station))
                data.paintScheme = entityManager.GetComponentData<StationStyle>(station).Scheme;
            if (entityManager.HasComponent<StationStats>(station))
                data.stats = StatsSaveData.From(entityManager.GetComponentData<StationStats>(station));
            if (entityManager.HasComponent<Achievements>(station))
                data.achievements = (long)entityManager.GetComponentData<Achievements>(station).Unlocked;

            if (entityManager.HasComponent<Finance>(station))
            {
                var difficulty = entityManager.HasComponent<StationRules>(station)
                    ? entityManager.GetComponentData<StationRules>(station).Difficulty
                    : Difficulty.Normal;
                data.finance = FinanceSaveData.From(entityManager.GetComponentData<Finance>(station), difficulty);
            }
            if (entityManager.HasComponent<Competitor>(station))
                data.competitor = CompetitorSaveData.From(entityManager.GetComponentData<Competitor>(station));

            CaptureShop(entityManager, data);
            CaptureProps(entityManager, data);
            CaptureVisitors(entityManager, station, data);
            CaptureContracts(entityManager, station, data);
            CaptureDiner(entityManager, data);
            CaptureGrowth(entityManager, station, data);
            data.stationName = GasStation.Bridge.StationProfile.CustomName;

            if (entityManager.HasBuffer<DayHistoryEntry>(station))
            {
                var history = entityManager.GetBuffer<DayHistoryEntry>(station, true);
                data.history = new DayHistorySaveData[history.Length];
                for (int i = 0; i < history.Length; i++)
                    data.history[i] = DayHistorySaveData.From(history[i]);
            }

            using (var renovationQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Renovation>()))
            using (var renovations = renovationQuery.ToComponentDataArray<Renovation>(Allocator.Temp))
            {
                var done = new System.Collections.Generic.List<int>();
                foreach (var renovation in renovations)
                {
                    if (renovation.Done)
                        done.Add(renovation.Id);
                }

                data.renovationsDone = done.ToArray();
            }

            using (var motelQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Motel>()))
            {
                if (motelQuery.CalculateEntityCount() == 1)
                {
                    var rooms = entityManager.GetBuffer<MotelRoom>(motelQuery.GetSingletonEntity(), true);
                    data.motelRoomsDirty = new bool[rooms.Length];
                    for (int i = 0; i < rooms.Length; i++)
                        data.motelRoomsDirty[i] = rooms[i].Dirty;
                }
            }

            using (var workerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Worker>()))
            using (var workers = workerQuery.ToComponentDataArray<Worker>(Allocator.Temp))
            {
                data.workers = new WorkerSaveData[workers.Length];
                for (int i = 0; i < workers.Length; i++)
                    data.workers[i] = WorkerSaveData.From(workers[i]);
            }

            using (var restroomQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Restroom>()))
            {
                if (restroomQuery.CalculateEntityCount() == 1)
                    data.restroomDirt = restroomQuery.GetSingleton<Restroom>().Dirt;
            }

            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Pump>()))
            using (var pumps = query.ToComponentDataArray<Pump>(Allocator.Temp))
            {
                data.pumps = new PumpSaveData[pumps.Length];
                for (int i = 0; i < pumps.Length; i++)
                    data.pumps[i] = new PumpSaveData { number = pumps[i].Number, condition = pumps[i].Condition };
            }

            using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Trash>(), ComponentType.ReadOnly<LocalToWorld>()))
            using (var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp))
            {
                data.trash = new TrashSaveData[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    float3 position = transforms[i].Position;
                    float3 forward = transforms[i].Forward;
                    data.trash[i] = new TrashSaveData
                    {
                        x = position.x,
                        y = position.y,
                        z = position.z,
                        yaw = math.atan2(forward.x, forward.z)
                    };
                }
            }

            return data;
        }

        /// <summary>Applies saved data and removes cars and pending deliveries.</summary>
        public static void Apply(EntityManager entityManager, Entity station, SaveData data)
        {
            ResetRuntimeState(entityManager);

            var economy = entityManager.GetComponentData<Economy>(station);
            var time = entityManager.GetComponentData<GameTime>(station);
            var upgrades = entityManager.GetComponentData<StationUpgrades>(station);

            var stockBuffer = entityManager.GetBuffer<FuelStock>(station);
            var stock = new FuelStock[stockBuffer.Length];
            for (int i = 0; i < stock.Length; i++)
                stock[i] = stockBuffer[i];

            data.ApplyTo(ref economy, ref time, ref upgrades, stock);
            var staff = data.RestoreStaff(ref upgrades, ref economy);

            for (int i = 0; i < stock.Length; i++)
                stockBuffer[i] = stock[i];
            entityManager.SetComponentData(station, economy);
            entityManager.SetComponentData(station, time);
            entityManager.SetComponentData(station, upgrades);
            RestoreStaff(entityManager, station, staff);

            if (entityManager.HasComponent<QuestProgress>(station))
                entityManager.SetComponentData(station, data.ToQuestProgress());

            if (entityManager.HasComponent<StationLevel>(station))
                entityManager.SetComponentData(station, data.ToStationLevel());
            if (entityManager.HasComponent<StationStats>(station))
                entityManager.SetComponentData(station, data.stats != null ? data.stats.ToStats() : default);
            if (entityManager.HasComponent<Achievements>(station))
                entityManager.SetComponentData(station, new Achievements { Unlocked = (ulong)data.achievements });
            if (data.paintScheme >= 0 && entityManager.HasComponent<StationStyle>(station))
                entityManager.SetComponentData(station, new StationStyle { Scheme = Mathf.Clamp(data.paintScheme, 0, 3) });

            if (data.pumps != null)
                RestorePumps(entityManager, data.pumps);

            RestoreProps(entityManager, data.props);
            RestoreVisitors(entityManager, station, data);
            RestoreContracts(entityManager, station, data);
            RestoreDiner(entityManager, data);
            RestoreGrowth(entityManager, station, data);

            if (data.products != null)
            {
                RestoreShop(entityManager, data.products);
                RestoreSupplier(entityManager, data.supplier);
            }

            if (data.restroomDirt >= 0f)
            {
                using var restroomQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Restroom>());
                if (restroomQuery.CalculateEntityCount() == 1)
                {
                    var restroomEntity = restroomQuery.GetSingletonEntity();
                    var restroom = entityManager.GetComponentData<Restroom>(restroomEntity);
                    restroom.Dirt = Mathf.Clamp01(data.restroomDirt);
                    entityManager.SetComponentData(restroomEntity, restroom);
                }
            }

            if (entityManager.HasBuffer<DayHistoryEntry>(station))
            {
                var history = entityManager.GetBuffer<DayHistoryEntry>(station);
                history.Clear();
                if (data.history != null)
                {
                    foreach (var entry in data.history)
                    {
                        if (entry != null)
                            history.Add(entry.ToEntry());
                    }
                }
            }

            GasStation.Bridge.StationProfile.SetName(data.stationName);

            if (entityManager.HasComponent<Finance>(station))
                entityManager.SetComponentData(station, data.finance != null ? data.finance.ToFinance() : default);
            if (entityManager.HasComponent<StationRules>(station))
                entityManager.SetComponentData(station, new StationRules
                {
                    Difficulty = data.finance != null ? data.finance.Difficulty : Difficulty.Normal
                });
            if (data.competitor != null && entityManager.HasComponent<Competitor>(station))
            {
                var rival = entityManager.GetComponentData<Competitor>(station);
                data.competitor.ApplyTo(ref rival);
                entityManager.SetComponentData(station, rival);
            }

            if (data.renovationsDone != null)
            {
                using var renovationQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Renovation>());
                using var entities = renovationQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                {
                    var renovation = entityManager.GetComponentData<Renovation>(entity);
                    renovation.Done = Array.IndexOf(data.renovationsDone, renovation.Id) >= 0;
                    entityManager.SetComponentData(entity, renovation);
                }
            }

            if (data.motelRoomsDirty != null)
            {
                using var motelQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Motel>());
                if (motelQuery.CalculateEntityCount() == 1)
                {
                    var rooms = entityManager.GetBuffer<MotelRoom>(motelQuery.GetSingletonEntity());
                    for (int i = 0; i < rooms.Length && i < data.motelRoomsDirty.Length; i++)
                    {
                        var room = rooms[i];
                        room.Dirty = data.motelRoomsDirty[i];
                        rooms[i] = room;
                    }
                }
            }

            if (data.trash != null)
                RestoreTrash(entityManager, data.trash);
        }

        private static void RestoreStaff(EntityManager entityManager, Entity station, System.Collections.Generic.List<Worker> staff)
        {
            using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Worker>()))
                entityManager.DestroyEntity(existing);

            int nextId = 1;
            foreach (var worker in staff)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, worker);
                nextId = Math.Max(nextId, worker.Id + 1);
            }

            if (entityManager.HasComponent<StaffRoster>(station))
            {
                var roster = entityManager.GetComponentData<StaffRoster>(station);
                roster.NextId = Math.Max(roster.NextId, nextId);
                entityManager.SetComponentData(station, roster);
            }
        }

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

        private static void RestorePumps(EntityManager entityManager, PumpSaveData[] saved)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Pump>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var pump = entityManager.GetComponentData<Pump>(entity);
                foreach (var entry in saved)
                {
                    if (entry != null && entry.number == pump.Number)
                        pump.Condition = Mathf.Clamp01(entry.condition);
                }

                entityManager.SetComponentData(entity, pump);
            }
        }

        private static void RestoreTrash(EntityManager entityManager, TrashSaveData[] trash)
        {
            using var spawnerQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TrashSpawner>());
            if (spawnerQuery.CalculateEntityCount() != 1)
                return;

            var spawnerEntity = spawnerQuery.GetSingletonEntity();
            var prefabBuffer = entityManager.GetBuffer<TrashPrefabElement>(spawnerEntity, true);
            if (prefabBuffer.Length == 0)
                return;

            // Copy first: instantiating invalidates the buffer.
            using var prefabs = prefabBuffer.ToNativeArray(Allocator.Temp);

            using (var existing = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Trash>()))
                entityManager.DestroyEntity(existing);

            for (int i = 0; i < trash.Length; i++)
            {
                var prefab = prefabs[i % prefabs.Length].Prefab;
                float scale = entityManager.HasComponent<LocalTransform>(prefab)
                    ? entityManager.GetComponentData<LocalTransform>(prefab).Scale
                    : 1f;

                var entity = entityManager.Instantiate(prefab);
                entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(trash[i].x, trash[i].y, trash[i].z), quaternion.RotateY(trash[i].yaw), scale));
                entityManager.AddComponentData(entity, new Trash());
            }
        }

        public static void Write(SaveData data)
        {
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            File.Copy(temp, path, true);
            File.Delete(temp);
        }

        public static bool TryRead(out SaveData data)
        {
            data = null;
            if (!Exists)
                return false;

            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data != null && data.IsSupported;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"GasStation: could not read the save file: {exception.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            if (Exists)
                File.Delete(FilePath);
        }

        private static void ResetRuntimeState(EntityManager entityManager)
        {
            using (var cars = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Car>()))
                entityManager.DestroyEntity(cars);

            using (var deliveries = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FuelDelivery>()))
                entityManager.DestroyEntity(deliveries);

            using (var productDeliveries = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductDelivery>()))
                entityManager.DestroyEntity(productDeliveries);

            using (var pedestrians = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Pedestrian>()))
                entityManager.DestroyEntity(pedestrians);

            using (var trucks = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DeliveryTruck>()))
                entityManager.DestroyEntity(trucks);

            using (var parkingQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TruckParking>()))
            using (var parkings = parkingQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var parkingEntity in parkings)
                {
                    var spots = entityManager.GetBuffer<ParkingSpot>(parkingEntity);
                    for (int i = 0; i < spots.Length; i++)
                    {
                        var spot = spots[i];
                        spot.Occupant = Entity.Null;
                        spots[i] = spot;
                    }
                }
            }

            using (var motelQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Motel>()))
            using (var motels = motelQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var motelEntity in motels)
                {
                    var rooms = entityManager.GetBuffer<MotelRoom>(motelEntity);
                    for (int i = 0; i < rooms.Length; i++)
                    {
                        var room = rooms[i];
                        room.Occupant = Entity.Null;
                        rooms[i] = room;
                    }
                }
            }

            using (var tireQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TireService>()))
            using (var tireServices = tireQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var serviceEntity in tireServices)
                {
                    var service = entityManager.GetComponentData<TireService>(serviceEntity);
                    service.Occupant = Entity.Null;
                    entityManager.SetComponentData(serviceEntity, service);
                }
            }

            using (var washQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<CarWash>()))
            using (var washes = washQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var washEntity in washes)
                {
                    var wash = entityManager.GetComponentData<CarWash>(washEntity);
                    wash.Occupant = Entity.Null;
                    entityManager.SetComponentData(washEntity, wash);
                }
            }

            using (var pumpQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<Pump>()))
            using (var pumps = pumpQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var pumpEntity in pumps)
                {
                    var pump = entityManager.GetComponentData<Pump>(pumpEntity);
                    pump.Occupant = Entity.Null;
                    entityManager.SetComponentData(pumpEntity, pump);
                }
            }
        }
    }
}
