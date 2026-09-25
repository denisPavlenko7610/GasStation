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
        Renovate,
        Laptop,
        Grill
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

    /// <summary>A card above a customer's car: what they want and how patient they still are.</summary>
    public struct CarCard
    {
        public UnityEngine.Vector3 Position;
        public CustomerType Customer;
        public CarState State;
        public FuelType Fuel;
        public float RequestedLiters;
        public float ReceivedLiters;
        public float PatienceRatio;
        /// <summary>1-based regular id; 0 = a stranger.</summary>
        public int RegularId;
        public int ContractId;
    }

    /// <summary>A worker's body on the lot, for StaffPresenter.</summary>
    public struct StaffBody
    {
        public int WorkerId;
        public StaffRole Role;
        public UnityEngine.Vector3 Position;
        public UnityEngine.Quaternion Rotation;
        public AgentJob Job;
        /// <summary>Off shift and gone home: not drawn.</summary>
        public bool Away;
    }

    public struct Review
    {
        public int Stars;
        /// <summary>1-based id of the regular who wrote it; 0 = anonymous.</summary>
        public int RegularId;
        /// <summary>Which of the texts for this star count.</summary>
        public int Variant;
        public int Day;
        public float Hour;
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

        /// <summary>Where the current quest wants the player to go (shown as a marker).</summary>
        public static bool HasQuestTarget;
        public static UnityEngine.Vector3 QuestTarget;

        public static readonly List<CarCard> Cards = new();
        public static readonly List<DayHistoryEntry> History = new();

        /// <summary>Newest first; the last MaxReviews reviews.</summary>
        public static readonly List<Review> Reviews = new();
        public const int MaxReviews = 8;

        public static StationStats Stats;
        public static Achievements Achievements;

        public static bool HasRestroom;
        public static float RestroomDirt;
        public static WorldEvents World;

        public static Finance Finance;
        public static Difficulty Difficulty = Difficulty.Normal;
        public static Competitor Competitor;
        /// <summary>Utility bill of the last night.</summary>
        public static float LastUtilities;
        /// <summary>The station went bankrupt; the menu shows the game over screen.</summary>
        public static bool GameOver;

        /// <summary>Placed props, in no particular order. PropPresenter draws them.</summary>
        public static readonly List<PlacedProp> Props = new();
        public static bool HasBuildArea;
        public static BuildArea BuildArea;
        public static readonly List<NoBuildZone> NoBuildZones = new();
        public static PropEffects PropEffects;

        public static readonly List<RegularState> Regulars = new();
        public static Buzz Buzz;

        public static readonly List<ContractOffer> Offers = new();
        public static readonly List<Contract> Contracts = new();
        public static bool HasLaptop;
        public static UnityEngine.Vector3 LaptopPosition;
        public static UnityEngine.Vector3 PlayerPosition;
        public const float LaptopRadius = 2.5f;

        public static readonly List<StaffBody> StaffBodies = new();

        public static SupplierKind Supplier;

        public static OwnerSkillSet Skills;
        public static StationStars Stars;
        public static RoadEvent Road;
        public static HostedEvents Hosted;

        public static bool HasDiner;
        public static Diner Diner;
        public static readonly DinerCounter[] DinerCounter = new DinerCounter[DinerDishes.Count];
        public static int ChargersOpen;
        public static int ChargersUsed;

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
            Cards.Clear();
            History.Clear();
            Reviews.Clear();
            AnyFueling = false;
            Upgrades = default;
            LastReport = default;
            Finance = default;
            Props.Clear();
            Offers.Clear();
            StaffBodies.Clear();
            Contracts.Clear();
            HasLaptop = false;
            Regulars.Clear();
            Buzz = default;
            NoBuildZones.Clear();
            HasBuildArea = false;
            Competitor = default;
            Difficulty = Difficulty.Normal;
            LastUtilities = 0f;
            GameOver = false;
            Message = null;
            MessageTime = float.NegativeInfinity;
        }
    }
}
