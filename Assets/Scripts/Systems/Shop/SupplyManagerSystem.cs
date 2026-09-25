using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>
    /// The SupplyManager upgrade reorders fuel and shop products when they run low,
    /// keeping a money reserve. Higher levels get a discount and faster deliveries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(StationCommandSystem))]
    public partial struct SupplyManagerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyManagerState>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<FuelStock>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int level = SystemAPI.GetSingleton<StationUpgrades>().SupplyManager;
            if (level == 0)
                return;

            ref var manager = ref SystemAPI.GetSingletonRW<SupplyManagerState>().ValueRW;
            manager.Timer += SystemAPI.Time.DeltaTime;
            if (manager.Timer < FacilityMath.SupplyCheckInterval)
                return;
            manager.Timer = 0f;

            float discount = FacilityMath.SupplyDiscount(level);
            float deliveryTime = SystemAPI.GetSingleton<StationSettings>().FuelDeliveryTime * FacilityMath.SupplyDeliveryFactor(level);
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var pendingFuel = new NativeArray<float>(FuelTypes.Count, Allocator.Temp);
            foreach (var delivery in SystemAPI.Query<RefRO<FuelDelivery>>())
                pendingFuel[(int)delivery.ValueRO.Type] += delivery.ValueRO.Liters;

            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);
            for (int i = 0; i < stock.Length && i < FuelTypes.Count; i++)
            {
                var fuel = stock[i];
                float available = fuel.Amount + pendingFuel[i];
                if (!FacilityMath.NeedsReorder(available, fuel.Capacity))
                    continue;

                float liters = math.min(fuel.Capacity * 0.5f, fuel.Capacity - available);
                float cost = liters * fuel.BuyPrice * discount;
                if (liters < 1f || economy.Money - cost < FacilityMath.MoneyReserve)
                    continue;

                economy.Money -= cost;
                economy.DayExpenses += cost;
                var order = ecb.CreateEntity();
                ecb.AddComponent(order, new FuelDelivery { Type = (FuelType)i, Liters = liters, TimeLeft = deliveryTime });
                StationEvent.Push(events, StationEventType.FuelOrdered, (FuelType)i, liters);
                StationEvent.Push(events, StationEventType.AutoOrder, (FuelType)i, cost);
            }

            if (SystemAPI.HasSingleton<Shop>())
            {
                var shopEntity = SystemAPI.GetSingletonEntity<Shop>();
                float productDelivery = SystemAPI.GetComponent<Shop>(shopEntity).DeliveryTime * FacilityMath.SupplyDeliveryFactor(level);
                var pendingProducts = new NativeArray<int>(ProductTypes.Count, Allocator.Temp);
                foreach (var delivery in SystemAPI.Query<RefRO<ProductDelivery>>())
                    pendingProducts[(int)delivery.ValueRO.Type] += delivery.ValueRO.Count;

                var shelves = SystemAPI.GetBuffer<ShopProduct>(shopEntity);
                for (int i = 0; i < shelves.Length && i < ProductTypes.Count; i++)
                {
                    var shelf = shelves[i];
                    int available = shelf.Stock + pendingProducts[i];
                    if (!FacilityMath.NeedsReorder(available, shelf.Capacity))
                        continue;

                    int count = math.min(ShopMath.OrderSize, shelf.Capacity - available);
                    float cost = count * shelf.BuyPrice * discount;
                    if (count <= 0 || economy.Money - cost < FacilityMath.MoneyReserve)
                        continue;

                    economy.Money -= cost;
                    economy.DayExpenses += cost;
                    var order = ecb.CreateEntity();
                    ecb.AddComponent(order, new ProductDelivery { Type = (ProductType)i, Count = count, TimeLeft = productDelivery });
                    StationEvent.Push(events, StationEventType.ProductsOrdered, default, count);
                    StationEvent.Push(events, StationEventType.AutoOrder, default, cost);
                }

                pendingProducts.Dispose();
            }

            pendingFuel.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
