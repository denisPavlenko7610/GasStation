using System;
using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Global station state: money, clock and fuel tanks. Exactly one per world.</summary>
    public class StationAuthoring : MonoBehaviour
    {
        [Serializable]
        public class FuelSettings
        {
            public FuelType type;
            public float capacity = 2000f;
            public float startAmount = 1000f;
            public float buyPrice = 1f;
            public float sellPrice = 1.4f;
            public float marketPrice = 1.4f;
        }

        [Header("Economy")]
        public float startMoney = 1000f;
        [Range(0f, 1f)] public float startReputation = 0.5f;
        public float dailyFixedCosts = 150f;

        [Header("Time")]
        [Range(0f, 24f)] public float startHour = 7f;
        public float minutesPerSecond = 1f;

        [Header("Service")]
        public float fuelDeliveryTime = 30f;
        public float interactionRadius = 4f;
        [Tooltip("Litter count at which cleanliness drops to 0%")]
        public int dirtyThreshold = 40;

        public FuelSettings[] fuels =
        {
            new() { type = FuelType.Petrol92, buyPrice = 1.00f, sellPrice = 1.40f, marketPrice = 1.40f },
            new() { type = FuelType.Petrol95, buyPrice = 1.10f, sellPrice = 1.55f, marketPrice = 1.55f },
            new() { type = FuelType.Diesel, buyPrice = 1.05f, sellPrice = 1.50f, marketPrice = 1.50f },
        };
    }

    public class StationBaker : Baker<StationAuthoring>
    {
        public override void Bake(StationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new Economy
            {
                Money = authoring.startMoney,
                Reputation = authoring.startReputation,
                DailyFixedCosts = authoring.dailyFixedCosts
            });
            AddComponent(entity, new DayReport());
            AddComponent(entity, new GameTime
            {
                Hour = authoring.startHour,
                Day = 1,
                MinutesPerSecond = authoring.minutesPerSecond
            });
            AddComponent(entity, new StationSettings
            {
                FuelDeliveryTime = authoring.fuelDeliveryTime,
                InteractionRadius = authoring.interactionRadius,
                DirtyThreshold = authoring.dirtyThreshold
            });

            AddComponent(entity, new StationUpgrades());
            AddComponent(entity, new StationCleanliness { Value = 1f });
            AddComponent(entity, new QuestProgress());
            AddBuffer<StationEvent>(entity);

            var stock = AddBuffer<FuelStock>(entity);
            for (int i = 0; i < FuelTypes.Count; i++)
                stock.Add(ToStock(Find(authoring, (FuelType)i)));
        }

        private static StationAuthoring.FuelSettings Find(StationAuthoring authoring, FuelType type)
        {
            if (authoring.fuels != null)
            {
                foreach (var fuel in authoring.fuels)
                {
                    if (fuel != null && fuel.type == type)
                        return fuel;
                }
            }

            return new StationAuthoring.FuelSettings { type = type };
        }

        private static FuelStock ToStock(StationAuthoring.FuelSettings settings) => new()
        {
            Amount = Mathf.Min(settings.startAmount, settings.capacity),
            Capacity = settings.capacity,
            BuyPrice = settings.buyPrice,
            SellPrice = settings.sellPrice,
            MarketPrice = settings.marketPrice
        };
    }
}
