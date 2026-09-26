using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Finds the unfinished renovation next to the player and, on interact, pays for it and marks it done.
    /// Runs after fueling, tires, repair and the restroom, and before litter pickup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(RestroomSystem))]
    [UpdateBefore(typeof(TrashPickupSystem))]
    public partial struct RenovationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Renovation>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                var nearest = Entity.Null;
                float best = float.MaxValue;
                float2 player = transform.ValueRO.Position.xz;

                foreach (var (renovation, entity) in SystemAPI.Query<RefRO<Renovation>>().WithEntityAccess())
                {
                    if (renovation.ValueRO.Done)
                        continue;

                    float distance = InteractionMath.Score(player, renovation.ValueRO.Position.xz, interaction.ValueRO.LookDirection,
                        RenovationMath.InteractionRadius);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = entity;
                    }
                }

                interaction.ValueRW.NearbyRenovation = nearest;
                if (!interaction.ValueRO.InteractPressed || nearest == Entity.Null)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                Renovate(ref state, nearest);
            }
        }

        private void Renovate(ref SystemState state, Entity entity)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var renovation = SystemAPI.GetComponentRW<Renovation>(entity);
            var kind = renovation.ValueRO.Kind;

            int level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            if (level < RenovationMath.RequiredLevel(kind))
            {
                StationEvent.Push(events, StationEventType.RenovationNeedsLevel, default, RenovationMath.RequiredLevel(kind));
                return;
            }

            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            float cost = RenovationMath.Cost(kind);
            if (economy.Money < cost)
            {
                StationEvent.Push(events, StationEventType.NotEnoughMoney, default, cost);
                return;
            }

            economy.Money -= cost;
            economy.DayExpenses += cost;
            economy.Reputation = StationMath.ClampReputation(economy.Reputation + RenovationMath.ReputationBonus);
            renovation.ValueRW.Done = true;
            StationEvent.Push(events, StationEventType.RenovationDone, default, (float)kind);
        }
    }
}
