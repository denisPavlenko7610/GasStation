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
        WorkerStole
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

        public static void Push(DynamicBuffer<StationEvent> buffer, StationEventType type, FuelType fuel = default, float value = 0f)
        {
            if (buffer.Length < MaxPending)
                buffer.Add(new StationEvent { Type = type, Fuel = fuel, Value = value });
        }
    }
}
