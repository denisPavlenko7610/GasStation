using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Applies the owner's "long arms": interaction and pickup radius follow the learned skills.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateBefore(typeof(PlayerInteractionSystem))]
    public partial struct OwnerSkillSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OwnerSkillSet>();
            state.RequireForUpdate<StationSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var skills = ref SystemAPI.GetSingletonRW<OwnerSkillSet>().ValueRW;
            ref var settings = ref SystemAPI.GetSingletonRW<StationSettings>().ValueRW;
            if (skills.BaseInteractionRadius <= 0f)
                skills.BaseInteractionRadius = settings.InteractionRadius;

            float reach = SkillMath.ReachFactor(skills.Learned);
            settings.InteractionRadius = skills.BaseInteractionRadius * reach;

            if (!SystemAPI.HasSingleton<TrashSpawner>())
                return;

            ref var trash = ref SystemAPI.GetSingletonRW<TrashSpawner>().ValueRW;
            if (skills.BasePickupRadius <= 0f)
                skills.BasePickupRadius = trash.PickupRadius;
            trash.PickupRadius = skills.BasePickupRadius * reach;
        }
    }
}
