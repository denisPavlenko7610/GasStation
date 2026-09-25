using GasStation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(StationCommandSystem))]
    public partial struct FuelDeliverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FuelStock>();
            state.RequireForUpdate<FuelDelivery>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>();
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (delivery, entity) in SystemAPI.Query<RefRW<FuelDelivery>>().WithEntityAccess())
            {
                // A delivery truck on its way completes the delivery itself when it has unloaded.
                var truck = delivery.ValueRO.Truck;
                if (truck != Entity.Null && SystemAPI.Exists(truck))
                    continue;

                delivery.ValueRW.TimeLeft -= deltaTime;
                if (delivery.ValueRO.TimeLeft > 0f)
                    continue;

                int index = (int)delivery.ValueRO.Type;
                var entry = stock[index];
                entry.Amount = math.min(entry.Capacity, entry.Amount + delivery.ValueRO.Liters);
                stock[index] = entry;
                StationEvent.Push(events, StationEventType.FuelDelivered, delivery.ValueRO.Type, delivery.ValueRO.Liters);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
