using System;
using System.IO;
using GasStation.Components;
using Unity.Collections;
using Unity.Entities;
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

            return SaveData.Create(
                entityManager.GetComponentData<Economy>(station),
                entityManager.GetComponentData<GameTime>(station),
                entityManager.GetComponentData<StationUpgrades>(station),
                stock,
                pending);
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
                return data != null && data.version == SaveData.CurrentVersion;
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
