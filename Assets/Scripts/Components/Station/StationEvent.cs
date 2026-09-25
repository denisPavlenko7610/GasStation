using Unity.Entities;

namespace GasStation.Components
{
    public enum StationEventType : byte
    {
        FuelingStarted,
        CustomerPaid,
        CustomerLeftAngry,
        FuelRanOut,
        FuelDelivered,
        DayEnded,
        UpgradeBought,
        FuelOrdered,
        TrashCollected,
        QuestCompleted,
        PumpBroken,
        PumpRepaired,
        LevelUp,
        TipReceived,
        FuelStolen,
        ThiefCaught,
        MarketChanged,
        Vandals,
        RushHourStarted,
        SandstormStarted,
        InspectionPassed,
        InspectionFailed,
        NotEnoughMoney,
        ShopSale,
        ShopEmpty,
        ProductsOrdered,
        ProductsDelivered,
        CarWashed,
        TruckParked,
        ParkingPaid,
        RestroomUsed,
        RestroomDisgusting,
        RestroomCleaned,
        AutoOrder,
        TiresChanged,
        StationPainted,
        WorkerHired,
        WorkerFired,
        WorkerStole,
        MotelCheckIn,
        MotelPaid,
        MotelRoomCleaned,
        AchievementUnlocked,
        RenovationDone,
        RenovationNeedsLevel,
        TruckArrived,
        /// <summary>Value = stars 1..5.</summary>
        CustomerReview,
        LoanTaken,
        LoanRepaid,
        LoanPayment,
        UtilitiesPaid,
        TaxPaid,
        InsurancePremiumPaid,
        InsurancePayout,
        BankruptcyWarning,
        GameOver,
        CompetitorOpened,
        CompetitorPriceCut,
        CompetitorPriceRise,
        CompetitorPromoStarted,
        CompetitorBoughtOut,
        /// <summary>Value = PropType.</summary>
        PropPlaced,
        /// <summary>Subject = regular id (1-based); Value = 1 on the very first visit.</summary>
        RegularArrived,
        /// <summary>Subject = regular id; Value = stars. Pushed right after the matching CustomerReview.</summary>
        RegularVisit,
        /// <summary>Subject = regular id: upset three times, now drives to the competitor.</summary>
        RegularLost,
        /// <summary>Subject = regular id: loyalty reached the top.</summary>
        RegularBestFriend,
        /// <summary>Value = stars the incognito critic gave.</summary>
        CriticVisit,
        /// <summary>Value = traffic factor of the article (above 1 = praise).</summary>
        CriticArticle,
        /// <summary>Value = CustomerType of the special guest; Subject = passengers or convoy size.</summary>
        SpecialArrived,
        EmergencyServed,
        /// <summary>Subject = offer id.</summary>
        ContractOffered,
        /// <summary>Subject = contract id.</summary>
        ContractAccepted,
        /// <summary>Subject = contract id.</summary>
        ContractVehicleServed,
        /// <summary>Subject = contract id.</summary>
        ContractVehicleMissed,
        /// <summary>Subject = contract id; Value = penalty paid for a missed vehicle.</summary>
        ContractPenalty,
        /// <summary>Subject = contract id; Value = bonus paid.</summary>
        ContractCompleted,
        /// <summary>Subject = contract id; Value = ContractType.</summary>
        ContractCancelled,
        /// <summary>Subject = name index; Value = StaffRole.</summary>
        WorkerQuit,
        /// <summary>Subject = worker id.</summary>
        WorkerPraised,
        /// <summary>Subject = ProductType; Value = units written off.</summary>
        ProductsSpoiled,
        /// <summary>Subject = ProductType; Value = units the cheap supplier did not bring.</summary>
        ProductsShort,
        /// <summary>A shoplifter walked into the shop.</summary>
        SuspiciousCustomer,
        /// <summary>Value = worth of the goods they tried to take.</summary>
        ShoplifterCaught,
        /// <summary>Value = worth of the stolen goods.</summary>
        GoodsStolen,
        /// <summary>Value = money taken from the till.</summary>
        Robbery,
        RobberyPrevented,
        /// <summary>Value = fine; the inspector found expired food.</summary>
        InspectionExpiredGoods,
        /// <summary>Value = bill; Subject = kWh.</summary>
        EvCharged,
        /// <summary>Value = price of the dish; Subject = DinerDish.</summary>
        DinerSale,
        /// <summary>Subject = DinerDish; the player cooked it at the grill.</summary>
        DishCooked,
        /// <summary>Value = dishes thrown away; Subject = DinerDish.</summary>
        FoodWasted,
        /// <summary>The cook has nothing to cook with.</summary>
        DinerOutOfIngredients,
        /// <summary>Value = new star count.</summary>
        StarGained,
        /// <summary>Value = new star count.</summary>
        StarLost,
        /// <summary>Value = RoadEventKind.</summary>
        RoadEventStarted,
        /// <summary>Value = RoadEventKind.</summary>
        RoadEventEnded,
        /// <summary>Value = HostedEventKind.</summary>
        HostedEventStarted,
        /// <summary>Value = ticket income; Subject = preparation in percent.</summary>
        HostedEventEnded,
        /// <summary>Value = OwnerSkill.</summary>
        SkillLearned,
        /// <summary>Value = SeasonKind.</summary>
        SeasonChanged,
        /// <summary>Value = WeatherKind.</summary>
        WeatherChanged,
        /// <summary>Subject = plate id of a paying customer.</summary>
        PlateSeen,
        /// <summary>Subject = plate id seen for the first time.</summary>
        NewPlate
    }

    /// <summary>
    /// One-frame gameplay event, written by Burst systems into a buffer on the station entity
    /// and drained by HudBridgeSystem for UI and audio.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct StationEvent : IBufferElementData
    {
        private const int MaxPending = 128;

        public StationEventType Type;
        public FuelType Fuel;
        public float Value;
        /// <summary>Who the event is about, e.g. a regular id; 0 when unused.</summary>
        public int Subject;

        public static void Push(DynamicBuffer<StationEvent> buffer, StationEventType type, FuelType fuel = default, float value = 0f,
            int subject = 0)
        {
            if (buffer.Length < MaxPending)
                buffer.Add(new StationEvent { Type = type, Fuel = fuel, Value = value, Subject = subject });
        }
    }
}
