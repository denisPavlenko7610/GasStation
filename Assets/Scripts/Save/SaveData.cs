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
        // Version 18
        public float age;
        public bool promo;
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

        // Version 17: living staff. "living" is false in older saves: fresh energy and a neutral mood.
        public bool living;
        public int shift;
        public int trait;
        public float energy;
        public float mood;
        public int training;
        public int unhappyDays;
        public int praisedDay;

        public Worker ToWorker() => new()
        {
            Id = id,
            Role = (StaffRole)Mathf.Clamp(role, 0, StaffRoles.Count - 1),
            Skill = skill,
            Wage = wage,
            Honesty = Mathf.Clamp01(honesty),
            DaysWorked = daysWorked,
            NameIndex = nameIndex,
            Shift = (WorkShift)Mathf.Clamp(shift, 0, 1),
            Trait = (StaffTrait)Mathf.Clamp(trait, 0, StaffTraits.Count - 1),
            Energy = living ? Mathf.Clamp01(energy) : 1f,
            Mood = living ? Mathf.Clamp01(mood) : GasStation.Logic.StaffMath.StartMood,
            Training = Mathf.Max(0, training),
            UnhappyDays = Mathf.Max(0, unhappyDays),
            PraisedDay = living ? praisedDay : -1
        };

        public static WorkerSaveData From(Worker worker) => new()
        {
            id = worker.Id,
            role = (int)worker.Role,
            skill = worker.Skill,
            wage = worker.Wage,
            honesty = worker.Honesty,
            daysWorked = worker.DaysWorked,
            nameIndex = worker.NameIndex,
            living = true,
            shift = (int)worker.Shift,
            trait = (int)worker.Trait,
            energy = worker.Energy,
            mood = worker.Mood,
            training = worker.Training,
            unhappyDays = worker.UnhappyDays,
            praisedDay = worker.PraisedDay
        };
    }

    [Serializable]
    public class StatsSaveData
    {
        public int served;
        public int trashCollected;
        public float income;
        public int thievesCaught;
        public int motelGuests;
        public int truckersHosted;
        public int tiresChanged;
        public int carsWashed;
        public int perfectDays;
        public int daysPlayed;
        public float ratingSum;
        public int ratingCount;

        public static StatsSaveData From(StationStats stats) => new()
        {
            served = stats.Served,
            trashCollected = stats.TrashCollected,
            income = stats.Income,
            thievesCaught = stats.ThievesCaught,
            motelGuests = stats.MotelGuests,
            truckersHosted = stats.TruckersHosted,
            tiresChanged = stats.TiresChanged,
            carsWashed = stats.CarsWashed,
            perfectDays = stats.PerfectDays,
            daysPlayed = stats.DaysPlayed,
            ratingSum = stats.RatingSum,
            ratingCount = stats.RatingCount
        };

        public StationStats ToStats() => new()
        {
            Served = served,
            TrashCollected = trashCollected,
            Income = income,
            ThievesCaught = thievesCaught,
            MotelGuests = motelGuests,
            TruckersHosted = truckersHosted,
            TiresChanged = tiresChanged,
            CarsWashed = carsWashed,
            PerfectDays = perfectDays,
            DaysPlayed = daysPlayed,
            RatingSum = ratingSum,
            RatingCount = ratingCount
        };
    }

    [Serializable]
    public class DayHistorySaveData
    {
        public int day;
        public float income;
        public float expenses;
        public int served;
        public int lost;
        public float rating;

        public static DayHistorySaveData From(DayHistoryEntry entry) => new()
        {
            day = entry.Day,
            income = entry.Income,
            expenses = entry.Expenses,
            served = entry.Served,
            lost = entry.Lost,
            rating = entry.Rating
        };

        public DayHistoryEntry ToEntry() => new()
        {
            Day = day,
            Income = income,
            Expenses = expenses,
            Served = served,
            Lost = lost,
            Rating = rating
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

    [Serializable]
    public class FinanceSaveData
    {
        public int difficulty = (int)Difficulty.Normal;
        public int loan;
        public float loanBalance;
        public float weeklyPayment;
        public bool insured;
        public float electricityToday;
        public float waterToday;
        public float weekRevenue;
        public int daysInDebt;
        public bool bankrupt;

        public static FinanceSaveData From(Finance finance, Difficulty difficulty) => new()
        {
            difficulty = (int)difficulty,
            loan = (int)finance.Loan,
            loanBalance = finance.LoanBalance,
            weeklyPayment = finance.WeeklyPayment,
            insured = finance.Insured,
            electricityToday = finance.ElectricityToday,
            waterToday = finance.WaterToday,
            weekRevenue = finance.WeekRevenue,
            daysInDebt = finance.DaysInDebt,
            bankrupt = finance.Bankrupt
        };

        public Difficulty Difficulty => (Difficulty)Mathf.Clamp(difficulty, 0, 2);

        public Finance ToFinance() => new()
        {
            Loan = (LoanKind)Mathf.Clamp(loan, 0, 2),
            LoanBalance = Mathf.Max(0f, loanBalance),
            WeeklyPayment = Mathf.Max(0f, weeklyPayment),
            Insured = insured,
            ElectricityToday = electricityToday,
            WaterToday = waterToday,
            WeekRevenue = weekRevenue,
            DaysInDebt = Mathf.Max(0, daysInDebt),
            Bankrupt = bankrupt
        };
    }

    [Serializable]
    public class CompetitorSaveData
    {
        public bool active;
        public bool boughtOut;
        public int opensOnDay;
        public float[] prices;
        public float reputation;
        public int promo;
        public int promoDaysLeft;
        public float ourShare;

        public static CompetitorSaveData From(Competitor rival) => new()
        {
            active = rival.Active,
            boughtOut = rival.BoughtOut,
            opensOnDay = rival.OpensOnDay,
            prices = new[] { rival.Petrol92, rival.Petrol95, rival.Diesel },
            reputation = rival.Reputation,
            promo = (int)rival.Promo,
            promoDaysLeft = rival.PromoDaysLeft,
            ourShare = rival.OurShare
        };

        /// <summary>Keeps the random state of the scene competitor.</summary>
        public void ApplyTo(ref Competitor rival)
        {
            // An empty object (JsonUtility never gives null) means an older save: keep the scene competitor.
            if (opensOnDay <= 0)
                return;

            rival.Active = active;
            rival.BoughtOut = boughtOut;
            rival.OpensOnDay = opensOnDay;
            if (prices != null && prices.Length >= FuelTypes.Count)
            {
                rival.Petrol92 = prices[0];
                rival.Petrol95 = prices[1];
                rival.Diesel = prices[2];
            }

            rival.Reputation = Mathf.Clamp01(reputation);
            rival.Promo = (CompetitorPromo)Mathf.Clamp(promo, 0, 2);
            rival.PromoDaysLeft = Mathf.Max(0, promoDaysLeft);
            rival.OurShare = Mathf.Clamp01(ourShare);
        }
    }

    [Serializable]
    public class PropSaveData
    {
        public int type;
        public float x;
        public float z;
        public float yaw;

        public static PropSaveData From(PlacedProp prop) => new()
        {
            type = (int)prop.Type,
            x = prop.Position.x,
            z = prop.Position.z,
            yaw = prop.Yaw
        };

        public PlacedProp ToProp(int id) => new()
        {
            Id = id,
            Type = (PropType)Mathf.Clamp(type, 0, PropTypes.Count - 1),
            Position = new Unity.Mathematics.float3(x, 0f, z),
            Yaw = Mathf.Repeat(yaw, 360f)
        };
    }

    [Serializable]
    public class RegularSaveData
    {
        public float loyalty;
        public int visits;
        public int upsets;
        public bool lost;
        public int lastVisitDay;
        public float lastStars;

        public static RegularSaveData From(RegularState regular) => new()
        {
            loyalty = regular.Loyalty,
            visits = regular.Visits,
            upsets = regular.Upsets,
            lost = regular.Lost,
            lastVisitDay = regular.LastVisitDay,
            lastStars = regular.LastStars
        };

        public RegularState ToState() => new()
        {
            Loyalty = Mathf.Clamp01(loyalty),
            Visits = Mathf.Max(0, visits),
            Upsets = Mathf.Max(0, upsets),
            Lost = lost,
            LastVisitDay = lastVisitDay,
            LastStars = lastStars
        };
    }

    [Serializable]
    public class ContractSaveData
    {
        public int id;
        public int type;
        public float price;
        /// <summary>Days left of an active contract, or the length of an offer.</summary>
        public int days;
        public int expiresIn;
        public int served;
        public int missed;

        public static ContractSaveData From(ContractOffer offer) => new()
        {
            id = offer.Id, type = (int)offer.Type, price = offer.Price, days = offer.Days, expiresIn = offer.ExpiresIn
        };

        public static ContractSaveData From(Contract contract) => new()
        {
            id = contract.Id, type = (int)contract.Type, price = contract.Price, days = contract.DaysLeft,
            served = contract.Served, missed = contract.Missed
        };

        private ContractType Type => (ContractType)Mathf.Clamp(type, 0, ContractTypes.Count - 1);

        public ContractOffer ToOffer() => new()
        {
            Id = id, Type = Type, Price = price, Days = Mathf.Max(1, days), ExpiresIn = Mathf.Max(1, expiresIn)
        };

        /// <summary>Vehicles already sent today are not sent again (LastSentDay is today's).</summary>
        public Contract ToContract(int today, int hour) => new()
        {
            Id = id, Type = Type, Price = price, DaysLeft = Mathf.Max(1, days), Served = served, Missed = missed,
            LastSentDay = today, LastSentHour = hour
        };
    }

    /// <summary>Persistent part of the game state. Cars on the road are not saved.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 22;

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

        // Version 8. Null in older saves: rooms stay as in the scene.
        public bool[] motelRoomsDirty;

        // Version 9. Older saves start with empty statistics and no achievements.
        public StatsSaveData stats;
        public long achievements;

        // Version 10. Null in older saves: renovations stay as in the scene.
        public int[] renovationsDone;

        // Version 11
        public DayHistorySaveData[] history;
        public float dayRatingSum;
        public int dayRatingCount;

        // Version 12. Empty = the default name.
        public string stationName;

        // Version 13. Null in older saves: normal difficulty, no loan, the competitor keeps its scene state.
        public FinanceSaveData finance;
        public CompetitorSaveData competitor;

        // Version 14. Null or empty in older saves: no props.
        public PropSaveData[] props;

        // Version 15. Null in older saves: every regular is still a stranger, no article running.
        public RegularSaveData[] regulars;
        public float buzzFactor;
        public int buzzDays;
        public int lastCriticDay;
        public int lastBusDay;

        // Version 16. Null in older saves: no offers and no contracts yet.
        public ContractSaveData[] offers;
        public ContractSaveData[] contracts;
        public int nextContractId;
        public int daysToNextOffer;

        // Version 18. 0 (cheap) in older saves.
        public int supplier;

        // Version 19. -1 in older saves: the diner keeps its scene ingredients.
        public int dinerIngredients = -1;
        public int[] dinerReady;
        public float[] dinerHoursLeft;

        // Version 20. Zero in older saves: no skills, no stars, a quiet road, nothing planned.
        public int skills;
        public int stars;
        public int bestStars;
        public int roadEvent;
        public float roadHoursLeft;
        public int plannedEvent;
        public int plannedEventDay;
        public int lastHostedDay = -100;

        // Version 21. Older saves: spring, no plates, the default cat name.
        public int season;
        public int weather;
        public int plates;
        public string catName;

        // Version 22. Older saves are free play without a campaign.
        public int gameMode;
        public bool campaignActive;
        public int campaignChapter = 1;
        public float campaignRepaid;
        public int campaignOutcome;
        public bool campaignOfferAnswered;
        public bool campaignVictoryShown;

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
                dayLost = economy.DayLost,
                dayRatingSum = economy.DayRatingSum,
                dayRatingCount = economy.DayRatingCount
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
                    NameIndex = nextId * 5,
                    Energy = 1f,
                    Mood = GasStation.Logic.StaffMath.StartMood,
                    PraisedDay = -1
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
            economy.DayRatingSum = dayRatingSum;
            economy.DayRatingCount = dayRatingCount;

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
