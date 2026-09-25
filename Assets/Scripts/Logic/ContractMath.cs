using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public struct ContractInfo
    {
        public FuelType Fuel;
        /// <summary>Price as a share of the market price at the time of the offer.</summary>
        public float PriceShare;
        public int Days;
        public int RequiredLevel;
        public int VehiclesPerSlot;
        public float LitersMultiplier;
        /// <summary>Paid for every vehicle that left without fuel.</summary>
        public float Penalty;
        /// <summary>Paid when the contract runs to the end.</summary>
        public float Bonus;
        /// <summary>First hour of the schedule and the step between slots (0 = one slot a day).</summary>
        public int FirstHour;
        public int EveryHours;
        public int LastHour;
    }

    /// <summary>Contracts with local companies: steady business in exchange for obligations.</summary>
    public static class ContractMath
    {
        public const int MaxOffers = 3;
        public const int MaxActive = 3;
        public const int OfferLifetime = 2;
        public const int DaysBetweenOffers = 2;
        /// <summary>Missed vehicles that break the contract.</summary>
        public const int StrikesToCancel = 3;
        public const float CancelReputation = 0.05f;
        public const float CompleteReputation = 0.04f;
        /// <summary>Thieves that still come while a police contract runs.</summary>
        public const float PoliceThiefShare = 0.4f;

        public static ContractInfo Get(ContractType type) => type switch
        {
            ContractType.Police => new ContractInfo
            {
                Fuel = FuelType.Petrol95, PriceShare = 0.85f, Days = 10, RequiredLevel = 2, VehiclesPerSlot = 1,
                LitersMultiplier = 1.2f, Penalty = 100f, Bonus = 400f, FirstHour = 10, EveryHours = 8, LastHour = 18
            },
            ContractType.Delivery => new ContractInfo
            {
                Fuel = FuelType.Petrol92, PriceShare = 1f, Days = 5, RequiredLevel = 2, VehiclesPerSlot = 1,
                LitersMultiplier = 0.8f, Penalty = 60f, Bonus = 500f, FirstHour = 9, EveryHours = 2, LastHour = 17
            },
            ContractType.BusCompany => new ContractInfo
            {
                Fuel = FuelType.Diesel, PriceShare = 0.95f, Days = 7, RequiredLevel = 3, VehiclesPerSlot = 3,
                LitersMultiplier = 3f, Penalty = 150f, Bonus = 600f, FirstHour = 8, EveryHours = 0, LastHour = 8
            },
            _ => new ContractInfo
            {
                Fuel = FuelType.Petrol92, PriceShare = 0.95f, Days = 7, RequiredLevel = 1, VehiclesPerSlot = 2,
                LitersMultiplier = 1.1f, Penalty = 50f, Bonus = 350f, FirstHour = 7, EveryHours = 13, LastHour = 20
            }
        };

        /// <summary>Is this game hour one of the contract's slots?</summary>
        public static bool IsSlot(ContractType type, int hour)
        {
            var info = Get(type);
            if (hour < info.FirstHour || hour > info.LastHour)
                return false;
            return info.EveryHours <= 0 ? hour == info.FirstHour : (hour - info.FirstHour) % info.EveryHours == 0;
        }

        /// <summary>Vehicles a day, for the offer text.</summary>
        public static int VehiclesPerDay(ContractType type)
        {
            var info = Get(type);
            int slots = info.EveryHours <= 0 ? 1 : (info.LastHour - info.FirstHour) / info.EveryHours + 1;
            return slots * info.VehiclesPerSlot;
        }

        public static float OfferPrice(ContractType type, float marketPrice) =>
            math.round(marketPrice * Get(type).PriceShare * 100f) / 100f;

        /// <summary>Picks an offer type the station can handle; -1 when none is unlocked.</summary>
        public static int PickType(float random01, int stationLevel)
        {
            int unlocked = 0;
            for (int i = 0; i < ContractTypes.Count; i++)
            {
                if (Get((ContractType)i).RequiredLevel <= stationLevel)
                    unlocked++;
            }

            if (unlocked == 0)
                return -1;

            int pick = math.min((int)(random01 * unlocked), unlocked - 1);
            for (int i = 0; i < ContractTypes.Count; i++)
            {
                if (Get((ContractType)i).RequiredLevel > stationLevel)
                    continue;
                if (pick-- == 0)
                    return i;
            }

            return -1;
        }

        /// <summary>Cancelling by hand costs as much as three missed vehicles.</summary>
        public static float CancelPenalty(ContractType type) => Get(type).Penalty * StrikesToCancel;

        public static bool IsBroken(int missed) => missed >= StrikesToCancel;
    }
}
