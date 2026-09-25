using System.Collections.Generic;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Bridge
{
    public enum StationCommandType
    {
        ChangePrice,
        OrderFuel
    }

    public struct StationCommand
    {
        public StationCommandType Type;
        public FuelType Fuel;
        public float Value;
    }

    /// <summary>Commands from the UI layer, applied to ECS by StationCommandSystem.</summary>
    public static class StationCommands
    {
        private static readonly Queue<StationCommand> Queue = new();

        public static int Count => Queue.Count;

        public static void ChangePrice(FuelType fuel, float delta) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.ChangePrice, Fuel = fuel, Value = delta });

        public static void OrderFuel(FuelType fuel, float liters) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.OrderFuel, Fuel = fuel, Value = liters });

        public static bool TryDequeue(out StationCommand command) => Queue.TryDequeue(out command);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Queue.Clear();
    }
}
