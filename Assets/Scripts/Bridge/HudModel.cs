using System.Collections.Generic;
using GasStation.Components;
using GasStation.Logic;
using UnityEngine;

namespace GasStation.Bridge
{
    public enum InteractionHint
    {
        None,
        PumpFree,
        CarArriving,
        CanStartFueling,
        Fueling,
        Trash,
        Repair,
        Restroom,
        Tires,
        MotelRoom,
        Renovate
    }

    public struct PumpInfo
    {
        public int Number;
        public bool Locked;
        public float Condition;
        public CustomerType Customer;
        public bool Occupied;
        public CarState CarState;
        public FuelType FuelType;
        public float RequestedLiters;
        public float ReceivedLiters;
        public float PatienceRatio;
    }

    /// <summary>Snapshot of the simulation for the UI, filled by HudBridgeSystem every frame.</summary>
    public static class HudModel
    {
        public static bool HasStation;

        public static float Hour;
        public static int Day;
        public static Economy Economy;
        public static DayReport LastReport;

        public static readonly FuelStock[] Fuel = new FuelStock[FuelTypes.Count];
        public static readonly float[] PendingDelivery = new float[FuelTypes.Count];
        public static readonly List<PumpInfo> Pumps = new();
        public static StationUpgrades Upgrades;

        /// <summary>Events of the last simulation frame. Refilled every frame; read them in Update.</summary>
        public static readonly List<StationEvent> Events = new();
        public static bool AnyFueling;
        public static StationCleanliness Cleanliness;
        public static QuestDefinition Quest;
        public static float QuestProgress;
        public static StationLevel Level;

        public static bool HasShop;
        public static readonly ShopProduct[] Products = new ShopProduct[ProductTypes.Count];
        public static readonly int[] PendingProducts = new int[ProductTypes.Count];
        public static int PedestriansInShop;

        public static bool HasWash;
        public static bool WashBusy;
        public static float WashTimeLeft;

        public static bool HasParking;
        public static int ParkingOpen;
        public static int ParkingTotal;
        public static int ParkingUsed;

        public static bool HasTireService;
        public static bool TireCarWaiting;
        public static float TireTimeLeft;
        public static int PaintScheme;

        public static readonly List<Worker> Workers = new();
        public static readonly List<StaffCandidate> Candidates = new();
        public static StaffPower Staff;

        public static bool HasMotel;
        public static int MotelOpen;
        public static int MotelUsed;
        public static int MotelDirty;

        /// <summary>Bit i set = renovation with Id i is done.</summary>
        public static ulong RenovationsDone;
        public static int RenovationsTotal;
        public static RenovationKind NearRenovationKind;

        public static bool IsRenovated(int id) => id >= 0 && id < 64 && (RenovationsDone & (1UL << id)) != 0;

        public static StationStats Stats;
        public static Achievements Achievements;

        public static bool HasRestroom;
        public static float RestroomDirt;
        public static WorldEvents World;

        public static int QueueLength;
        public static int CarsOnSite;
        public static InteractionHint Hint;

        public static string Message { get; private set; }
        public static float MessageTime { get; private set; } = float.NegativeInfinity;

        public static void Notify(string message)
        {
            Message = message;
            MessageTime = Time.unscaledTime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            HasStation = false;
            Pumps.Clear();
            Events.Clear();
            AnyFueling = false;
            Upgrades = default;
            LastReport = default;
            Message = null;
            MessageTime = float.NegativeInfinity;
        }
    }
}
