using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(FuelDeliverySystem))]
    public partial struct ProductDeliverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Shop>();
            state.RequireForUpdate<ProductDelivery>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var shopEntity = SystemAPI.GetSingletonEntity<Shop>();
            var shelves = SystemAPI.GetBuffer<ShopProduct>(shopEntity);
            ref var shop = ref SystemAPI.GetComponentRW<Shop>(shopEntity).ValueRW;
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (delivery, entity) in SystemAPI.Query<RefRW<ProductDelivery>>().WithEntityAccess())
            {
                // A delivery truck on its way completes the delivery itself when it has unloaded.
                var truck = delivery.ValueRO.Truck;
                if (truck != Entity.Null && SystemAPI.Exists(truck))
                    continue;

                delivery.ValueRW.TimeLeft -= deltaTime;
                if (delivery.ValueRO.TimeLeft > 0f)
                    continue;

                int index = (int)delivery.ValueRO.Type;
                int count = delivery.ValueRO.Count;
                // The cheap supplier sometimes brings less than was paid for.
                if (delivery.ValueRO.Supplier == SupplierKind.Cheap && shop.Random.NextFloat() < ShopMath.ShortDeliveryChance)
                {
                    int delivered = (int)math.floor(count * ShopMath.ShortDeliveryShare);
                    StationEvent.Push(events, StationEventType.ProductsShort, default, count - delivered, index);
                    count = delivered;
                }

                if (index < shelves.Length)
                {
                    var shelf = shelves[index];
                    int added = math.min(shelf.Capacity - shelf.Stock, count);
                    shelf.Age = ShopMath.AgeAfterDelivery(shelf.Age, shelf.Stock, math.max(0, added));
                    shelf.Stock = math.min(shelf.Capacity, shelf.Stock + count);
                    shelves[index] = shelf;
                }

                StationEvent.Push(events, StationEventType.ProductsDelivered, default, count);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
