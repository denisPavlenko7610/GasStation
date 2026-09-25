using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>The player cleans a dirty restroom with interact next to its door; janitors come and clean it too.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(RepairSystem))]
    [UpdateBefore(typeof(TrashPickupSystem))]
    public partial struct RestroomSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Restroom>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var restroom = ref SystemAPI.GetSingletonRW<Restroom>().ValueRW;
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;

            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                if (!interaction.ValueRO.InteractPressed || restroom.Dirt <= FacilityMath.RestroomCleanThreshold)
                    continue;
                if (math.distancesq(transform.ValueRO.Position.xz, restroom.Door.xz) > radius * radius)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                restroom.Dirt = 0f;
                StationEvent.Push(SystemAPI.GetSingletonBuffer<StationEvent>(), StationEventType.RestroomCleaned);
            }

            // Janitors walk over and clean it themselves (StaffAgentSystem).
        }
    }
}
