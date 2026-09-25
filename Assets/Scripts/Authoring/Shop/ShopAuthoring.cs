using System;
using GasStation.Components;
using GasStation.Logic;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Put at the shop door. Drivers walk here after fueling and buy products.</summary>
    public class ShopAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ProductSettings
        {
            public ProductType type;
            public int startStock = 15;
            public int capacity = 40;
            public float buyPrice = 1f;
            public float sellPrice = 2f;
        }

        [Tooltip("Optional walking driver model. Without it the driver is invisible but shopping still takes time")]
        public GameObject pedestrianPrefab;
        [Tooltip("Seconds a customer spends inside")] public float shopTime = 8f;
        public float deliveryTime = 30f;
        public uint seed = 5;
        [Tooltip("Leave empty for default products")] public ProductSettings[] products;
    }

    public class ShopBaker : Baker<ShopAuthoring>
    {
        public override void Bake(ShopAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Shop
            {
                Door = GetComponent<Transform>().position,
                ShopTime = authoring.shopTime,
                DeliveryTime = authoring.deliveryTime,
                PedestrianPrefab = authoring.pedestrianPrefab != null
                    ? GetEntity(authoring.pedestrianPrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var shelves = AddBuffer<ShopProduct>(entity);
            for (int i = 0; i < ProductTypes.Count; i++)
            {
                var type = (ProductType)i;
                var defaults = ShopMath.Defaults(type);
                var shelf = new ShopProduct
                {
                    Stock = defaults.StartStock,
                    Capacity = defaults.Capacity,
                    BuyPrice = defaults.BuyPrice,
                    SellPrice = defaults.SellPrice,
                    ReferencePrice = defaults.SellPrice
                };

                if (authoring.products != null)
                {
                    foreach (var settings in authoring.products)
                    {
                        if (settings == null || settings.type != type)
                            continue;
                        shelf.Capacity = settings.capacity;
                        shelf.Stock = Mathf.Min(settings.startStock, settings.capacity);
                        shelf.BuyPrice = settings.buyPrice;
                        shelf.SellPrice = settings.sellPrice;
                    }
                }

                shelves.Add(shelf);
            }
        }
    }
}
