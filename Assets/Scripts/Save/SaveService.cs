using System;
using System.IO;
using GasStation.Components;
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

            CaptureShop(entityManager, data);

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

            if (data.products != null)
                RestoreShop(entityManager, data.products);

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
                    sellPrice = shelves[i].SellPrice
                };
            }
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
                shelves[i] = shelf;
            }
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
