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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (delivery, entity) in SystemAPI.Query<RefRW<FuelDelivery>>().WithEntityAccess())
            {
                delivery.ValueRW.TimeLeft -= deltaTime;
                if (delivery.ValueRO.TimeLeft > 0f)
                    continue;

                int index = (int)delivery.ValueRO.Type;
                var entry = stock[index];
                entry.Amount = math.min(entry.Capacity, entry.Amount + delivery.ValueRO.Liters);
                stock[index] = entry;
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
