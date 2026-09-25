using System.Collections.Generic;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Bridge
{
    public enum InteractionHint
    {
        None,
        PumpFree,
        CarArriving,
        CanStartFueling,
        Fueling
    }

    public struct PumpInfo
    {
        public int Number;
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
            LastReport = default;
            Message = null;
            MessageTime = float.NegativeInfinity;
        }
    }
}
