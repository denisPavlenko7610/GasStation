using GasStation.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Finds the pump and the litter next to the player. On interact, starting fueling has priority;
    /// otherwise the press is left for TrashPickupSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(QueueSystem))]
    [UpdateAfter(typeof(PlayerMoveSystem))]
    public partial struct PlayerInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float pumpRadius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            float trashRadius = SystemAPI.HasSingleton<TrashSpawner>()
                ? SystemAPI.GetSingleton<TrashSpawner>().PickupRadius
                : 2.5f;

            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                float2 playerPosition = transform.ValueRO.Position.xz;
                interaction.ValueRW.NearbyPump = FindNearestPump(ref state, playerPosition, pumpRadius);
                interaction.ValueRW.NearbyTrash = FindNearestTrash(ref state, playerPosition, trashRadius);

                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (!interaction.ValueRO.InteractPressed || pumpEntity == Entity.Null)
                    continue;

                var occupant = SystemAPI.GetComponent<Pump>(pumpEntity).Occupant;
                if (occupant == Entity.Null || !SystemAPI.Exists(occupant))
                    continue;

                var car = SystemAPI.GetComponentRW<Car>(occupant);
                if (car.ValueRO.State != CarState.WaitingForService)
                    continue;

                car.ValueRW.State = CarState.Fueling;
                interaction.ValueRW.InteractPressed = false;
                StationEvent.Push(SystemAPI.GetSingletonBuffer<StationEvent>(), StationEventType.FuelingStarted, car.ValueRO.FuelType);
            }
        }

        private Entity FindNearestPump(ref SystemState state, float2 position, float radius)
        {
            float best = radius * radius;
            var nearest = Entity.Null;
            foreach (var (pump, entity) in SystemAPI.Query<RefRO<Pump>>().WithEntityAccess())
            {
                float distance = math.distancesq(pump.ValueRO.InteractionPoint.xz, position);
                if (distance < best)
                {
                    best = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        private Entity FindNearestTrash(ref SystemState state, float2 position, float radius)
        {
            float best = radius * radius;
            var nearest = Entity.Null;
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<Trash>().WithEntityAccess())
            {
                float distance = math.distancesq(transform.ValueRO.Position.xz, position);
                if (distance < best)
                {
                    best = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }
    }
}
