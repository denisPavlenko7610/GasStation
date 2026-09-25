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

            for (int i = 0; i < stock.Length; i++)
                stockBuffer[i] = stock[i];
            entityManager.SetComponentData(station, economy);
            entityManager.SetComponentData(station, time);
            entityManager.SetComponentData(station, upgrades);

            if (entityManager.HasComponent<QuestProgress>(station))
                entityManager.SetComponentData(station, data.ToQuestProgress());

            if (entityManager.HasComponent<StationLevel>(station))
                entityManager.SetComponentData(station, data.ToStationLevel());

            if (data.pumps != null)
                RestorePumps(entityManager, data.pumps);

            if (data.trash != null)
                RestoreTrash(entityManager, data.trash);
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
                Debug.LogWarning($"GasStation: не удалось прочитать сохранение: {exception.Message}");
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
