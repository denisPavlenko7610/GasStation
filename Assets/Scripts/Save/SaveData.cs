using System;
using System.Collections.Generic;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Save
{
    [Serializable]
    public class FuelSaveData
    {
        public float amount;
        public float capacity;
        public float sellPrice;
        // Version 3; 0 in older saves means "keep the scene value".
        public float marketPrice;
        public float buyPrice;
    }

    [Serializable]
    public class ProductSaveData
    {
        public int stock;
        public int capacity;
        public float sellPrice;
    }

    [Serializable]
    public class WorkerSaveData
    {
        public int id;
        public int role;
        public float skill;
        public float wage;
        public float honesty;
        public int daysWorked;
        public int nameIndex;

        public Worker ToWorker() => new()
        {
            Id = id,
            Role = (StaffRole)Mathf.Clamp(role, 0, StaffRoles.Count - 1),
            Skill = skill,
            Wage = wage,
            Honesty = Mathf.Clamp01(honesty),
            DaysWorked = daysWorked,
            NameIndex = nameIndex
        };

        public static WorkerSaveData From(Worker worker) => new()
        {
            id = worker.Id,
            role = (int)worker.Role,
            skill = worker.Skill,
            wage = worker.Wage,
            honesty = worker.Honesty,
            daysWorked = worker.DaysWorked,
            nameIndex = worker.NameIndex
        };
    }

    [Serializable]
    public class PumpSaveData
    {
        public int number;
        public float condition;
    }

    [Serializable]
    public class TrashSaveData
    {
        public float x;
        public float y;
        public float z;
        public float yaw;
    }

    /// <summary>Persistent part of the game state. Cars on the road are not saved.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 7;

        public int version = CurrentVersion;
        public int day;
        public float hour;
        public float money;
        public float reputation;
        public float dailyFixedCosts;
        public float dayIncome;
        public float dayExpenses;
        public int dayServed;
        public int dayLost;
        public int[] upgrades = new int[UpgradeTypes.Count];
        public FuelSaveData[] fuel = new FuelSaveData[FuelTypes.Count];

        // Version 2
        public int questIndex;
        public float questCounter;
        public int questsCompleted;
        /// <summary>Null in version 1 saves: litter from the scene is kept as is.</summary>
        public TrashSaveData[] trash;

        // Version 3
        public int stationLevel;
        public float stationExperience;
        /// <summary>Null in older saves: pumps keep their scene condition.</summary>
        public PumpSaveData[] pumps;

        // Version 4
        /// <summary>Null in older saves: the shop keeps its scene stock.</summary>
        public ProductSaveData[] products;

        // Version 5; -1 means "no restroom saved" (older saves keep the scene value).
        public float restroomDirt = -1f;

        // Version 6; -1 means "keep the scene paint".
        public int paintScheme = -1;

        // Version 7. Null in older saves: staff is converted from the old Attendant/Janitor/Mechanic upgrades.
        public WorkerSaveData[] workers;

        public bool IsSupported => version >= 1 && version <= CurrentVersion;

        /// <param name="pendingDeliveries">Liters already paid for but not delivered, per fuel type. Saved as delivered.</param>
        public static SaveData Create(Economy economy, GameTime time, StationUpgrades upgrades,
            IReadOnlyList<FuelStock> stock, IReadOnlyList<float> pendingDeliveries)
        {
            var data = new SaveData
            {
                day = time.Day,
                hour = time.Hour,
                money = economy.Money,
                reputation = economy.Reputation,
                dailyFixedCosts = economy.DailyFixedCosts,
                dayIncome = economy.DayIncome,
                dayExpenses = economy.DayExpenses,
                dayServed = economy.DayServed,
                dayLost = economy.DayLost
            };

            for (int i = 0; i < UpgradeTypes.Count; i++)
                data.upgrades[i] = upgrades.Get((UpgradeType)i);

            for (int i = 0; i < FuelTypes.Count && i < stock.Count; i++)
            {
                float pending = pendingDeliveries != null && i < pendingDeliveries.Count ? pendingDeliveries[i] : 0f;
                data.fuel[i] = new FuelSaveData
                {
                    amount = Mathf.Min(stock[i].Capacity, stock[i].Amount + pending),
                    capacity = stock[i].Capacity,
                    sellPrice = stock[i].SellPrice,
                    marketPrice = stock[i].MarketPrice,
                    buyPrice = stock[i].BuyPrice
                };
            }

            return data;
        }

        public void CaptureQuest(QuestProgress quest)
        {
            questIndex = quest.Index;
            questCounter = quest.Counter;
            questsCompleted = quest.Completed;
        }

        /// <summary>
        /// Staff to restore. Saves before version 7 bought Attendant, Janitor and Mechanic as upgrades with a
        /// salary in dailyFixedCosts: each level becomes an average worker and the salary moves to wages.
        /// </summary>
        public List<Worker> RestoreStaff(ref StationUpgrades stationUpgrades, ref Economy economy)
        {
            var result = new List<Worker>();
            if (workers != null)
            {
                foreach (var worker in workers)
                {
                    if (worker != null)
                        result.Add(worker.ToWorker());
                }

                return result;
            }

            int nextId = 1;
            ConvertLegacy(UpgradeType.Attendant, StaffRole.Attendant, 80f, ref stationUpgrades, ref economy, result, ref nextId);
            ConvertLegacy(UpgradeType.Janitor, StaffRole.Janitor, 60f, ref stationUpgrades, ref economy, result, ref nextId);
            ConvertLegacy(UpgradeType.Mechanic, StaffRole.Mechanic, 70f, ref stationUpgrades, ref economy, result, ref nextId);
            return result;
        }

        private static void ConvertLegacy(UpgradeType type, StaffRole role, float oldSalary, ref StationUpgrades stationUpgrades,
            ref Economy economy, List<Worker> result, ref int nextId)
        {
            int level = stationUpgrades.Get(type);
            for (int i = 0; i < level; i++)
            {
                result.Add(new Worker
                {
                    Id = nextId++,
                    Role = role,
                    Skill = 1f,
                    Wage = oldSalary,
                    Honesty = 1f,
                    NameIndex = nextId * 5
                });
            }

            economy.DailyFixedCosts = Mathf.Max(0f, economy.DailyFixedCosts - level * oldSalary);
            stationUpgrades.Set(type, 0);
        }

        public void CaptureLevel(StationLevel level)
        {
            stationLevel = level.Level;
            stationExperience = level.Experience;
        }

        /// <summary>Older saves have no level and start at level 1.</summary>
        public StationLevel ToStationLevel() => new()
        {
            Level = Mathf.Max(1, stationLevel),
            Experience = Mathf.Max(0f, stationExperience)
        };

        public QuestProgress ToQuestProgress() => new()
        {
            Index = Mathf.Max(0, questIndex),
            Counter = questCounter,
            Completed = questsCompleted
        };

        public void ApplyTo(ref Economy economy, ref GameTime time, ref StationUpgrades stationUpgrades, IList<FuelStock> stock)
        {
            time.Day = Mathf.Max(1, day);
            time.Hour = Mathf.Repeat(hour, 24f);

            economy.Money = money;
            economy.Reputation = Mathf.Clamp01(reputation);
            economy.DailyFixedCosts = dailyFixedCosts;
            economy.DayIncome = dayIncome;
            economy.DayExpenses = dayExpenses;
            economy.DayServed = dayServed;
            economy.DayLost = dayLost;

            for (int i = 0; i < UpgradeTypes.Count; i++)
                stationUpgrades.Set((UpgradeType)i, upgrades != null && i < upgrades.Length ? upgrades[i] : 0);

            for (int i = 0; i < stock.Count && fuel != null && i < fuel.Length; i++)
            {
                if (fuel[i] == null)
                    continue;

                var entry = stock[i];
                entry.Capacity = fuel[i].capacity;
                entry.Amount = Mathf.Clamp(fuel[i].amount, 0f, fuel[i].capacity);
                entry.SellPrice = fuel[i].sellPrice;
                if (fuel[i].marketPrice > 0f)
                    entry.MarketPrice = fuel[i].marketPrice;
                if (fuel[i].buyPrice > 0f)
                    entry.BuyPrice = fuel[i].buyPrice;
                stock[i] = entry;
            }
        }
    }
}
