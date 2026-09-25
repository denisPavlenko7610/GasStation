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
        OrderProducts,
        PaintStation,
        HireCandidate,
        FireWorker,
        TakeLoan,
        RepayLoan,
        ToggleInsurance,
        BuyOutCompetitor,
        PlaceProp,
        RemoveProp,
        AcceptContract,
        TogglePromo,
        SetSupplier,
        PraiseWorker,
        TrainWorker,
        RaiseWage,
        ToggleShift,
        DeclineContract,
        CancelContract
    }

    public struct StationCommand
    {
        public StationCommandType Type;
        public FuelType Fuel;
        public UpgradeType Upgrade;
        public ProductType Product;
        public PropType Prop;
        public float Value;
        /// <summary>World position for build mode commands.</summary>
        public UnityEngine.Vector3 Position;
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

        public static void NewGame(Difficulty difficulty = Difficulty.Normal) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.NewGame, Value = (int)difficulty });

        public static void TakeLoan(LoanKind kind) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.TakeLoan, Value = (int)kind });

        public static void RepayLoan() => Queue.Enqueue(new StationCommand { Type = StationCommandType.RepayLoan });

        public static void ToggleInsurance() => Queue.Enqueue(new StationCommand { Type = StationCommandType.ToggleInsurance });

        public static void BuyOutCompetitor() => Queue.Enqueue(new StationCommand { Type = StationCommandType.BuyOutCompetitor });

        public static void ChangeProductPrice(ProductType product, float delta) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.ChangeProductPrice, Product = product, Value = delta });

        public static void OrderProducts(ProductType product, int count) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.OrderProducts, Product = product, Value = count });

        public static void PaintStation(int scheme) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.PaintStation, Value = scheme });

        public static void HireCandidate(int candidateIndex) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.HireCandidate, Value = candidateIndex });

        public static void FireWorker(int workerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.FireWorker, Value = workerId });

        public static void PlaceProp(PropType prop, Vector3 position, float yaw) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.PlaceProp, Prop = prop, Position = position, Value = yaw });

        public static void RemoveProp(Vector3 position) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.RemoveProp, Position = position });

        public static void TogglePromo(ProductType product) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.TogglePromo, Product = product });

        public static void SetSupplier(SupplierKind supplier) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.SetSupplier, Value = (int)supplier });

        public static void PraiseWorker(int workerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.PraiseWorker, Value = workerId });

        public static void TrainWorker(int workerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.TrainWorker, Value = workerId });

        public static void RaiseWage(int workerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.RaiseWage, Value = workerId });

        public static void ToggleShift(int workerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.ToggleShift, Value = workerId });

        public static void AcceptContract(int offerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.AcceptContract, Value = offerId });

        public static void DeclineContract(int offerId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.DeclineContract, Value = offerId });

        public static void CancelContract(int contractId) =>
            Queue.Enqueue(new StationCommand { Type = StationCommandType.CancelContract, Value = contractId });

        public static bool TryDequeue(out StationCommand command) => Queue.TryDequeue(out command);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Queue.Clear();
    }
}
