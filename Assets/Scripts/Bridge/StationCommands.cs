using System.Collections.Generic;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Bridge
{
    public enum StationCommandType
    {
        ChangePrice,
        OrderFuel,
        BuyUpgrade,
        SaveGame,
        LoadGame,
        NewGame,
        ChangeProductPrice,
        OrderProducts
    }

    public struct StationCommand
    {
        public StationCommandType Type;
        public FuelType Fuel;
        public UpgradeType Upgrade;
        public ProductType Product;
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

        public static void BuyUpgrade(UpgradeType upgrade) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.BuyUpgrade, Upgrade = upgrade });

        public static void SaveGame() => Queue.Enqueue(new StationCommand { Type = StationCommandType.SaveGame });

        public static void LoadGame() => Queue.Enqueue(new StationCommand { Type = StationCommandType.LoadGame });

        public static void NewGame() => Queue.Enqueue(new StationCommand { Type = StationCommandType.NewGame });

        public static void ChangeProductPrice(ProductType product, float delta) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.ChangeProductPrice, Product = product, Value = delta });

        public static void OrderProducts(ProductType product, int count) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.OrderProducts, Product = product, Value = count });

        public static bool TryDequeue(out StationCommand command) => Queue.TryDequeue(out command);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Queue.Clear();
    }
}
