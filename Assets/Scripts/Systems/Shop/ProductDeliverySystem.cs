using GasStation.Components;
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
            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (delivery, entity) in SystemAPI.Query<RefRW<ProductDelivery>>().WithEntityAccess())
            {
                delivery.ValueRW.TimeLeft -= deltaTime;
                if (delivery.ValueRO.TimeLeft > 0f)
                    continue;

                int index = (int)delivery.ValueRO.Type;
                if (index < shelves.Length)
                {
                    var shelf = shelves[index];
                    shelf.Stock = math.min(shelf.Capacity, shelf.Stock + delivery.ValueRO.Count);
                    shelves[index] = shelf;
                }

                StationEvent.Push(events, StationEventType.ProductsDelivered, default, delivery.ValueRO.Count);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
