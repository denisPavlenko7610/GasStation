using GasStation.Components;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Removes the litter next to the player when interact was not used for fueling.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    public partial struct TrashPickupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var toDestroy = Entity.Null;
            foreach (var interaction in SystemAPI.Query<RefRW<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var trash = interaction.ValueRO.NearbyTrash;
                if (!interaction.ValueRO.InteractPressed || trash == Entity.Null || !SystemAPI.Exists(trash))
                    continue;

                interaction.ValueRW.InteractPressed = false;
                interaction.ValueRW.NearbyTrash = Entity.Null;
                toDestroy = trash;
            }

            if (toDestroy == Entity.Null)
                return;

            StationEvent.Push(SystemAPI.GetSingletonBuffer<StationEvent>(), StationEventType.TrashCollected);
            state.EntityManager.DestroyEntity(toDestroy);
        }
    }
}
