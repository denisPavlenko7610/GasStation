using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public enum ProductType : byte
    {
        Water = 0,
        Coffee = 1,
        Snacks = 2,
        MotorOil = 3,
        Souvenir = 4
    }

    public static class ProductTypes
    {
        public const int Count = 5;
    }

    /// <summary>The shop in the station building. Drivers walk to Door and spend ShopTime inside.</summary>
    public struct Shop : IComponentData
    {
        public float3 Door;
        public float ShopTime;
        public Entity PedestrianPrefab;
        public float DeliveryTime;
        public Random Random;
    }

    /// <summary>Shelf stock of one product. Buffer index equals (int)ProductType.</summary>
    [InternalBufferCapacity(ProductTypes.Count)]
    public struct ShopProduct : IBufferElementData
    {
        public int Stock;
        public int Capacity;
        public float BuyPrice;
        public float SellPrice;
        /// <summary>Price customers consider fair.</summary>
        public float ReferencePrice;
    }

    public struct ProductDelivery : IComponentData
    {
        public ProductType Type;
        public int Count;
        public float TimeLeft;
    }

    public enum PedestrianState : byte
    {
        ToShop,
        InShop,
        ToCar
    }

    public struct Pedestrian : IComponentData
    {
        public PedestrianState State;
        public Entity Car;
        public float Timer;
    }

    /// <summary>A single-bay car wash, available after the CarWash upgrade.</summary>
    public struct CarWash : IComponentData
    {
        public float3 Bay;
        public quaternion BayRotation;
        public float Duration;
        public float Price;
        public Entity Occupant;
    }

    public struct WashEntryPoint : IBufferElementData
    {
        public float3 Position;
    }

    public struct WashExitPoint : IBufferElementData
    {
        public float3 Position;
    }
}
