using GasStation.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Finds the pump next to the player and starts fueling on interact.</summary>
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;

            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                float2 playerPosition = transform.ValueRO.Position.xz;
                float bestDistance = radius * radius;
                var nearest = Entity.Null;

                foreach (var (pump, pumpEntity) in SystemAPI.Query<RefRO<Pump>>().WithEntityAccess())
                {
                    float distance = math.distancesq(pump.ValueRO.InteractionPoint.xz, playerPosition);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        nearest = pumpEntity;
                    }
                }

                interaction.ValueRW.NearbyPump = nearest;
                if (!interaction.ValueRO.InteractPressed || nearest == Entity.Null)
                    continue;

                var occupant = SystemAPI.GetComponent<Pump>(nearest).Occupant;
                if (occupant == Entity.Null || !SystemAPI.Exists(occupant))
                    continue;

                var car = SystemAPI.GetComponentRW<Car>(occupant);
                if (car.ValueRO.State == CarState.WaitingForService)
                    car.ValueRW.State = CarState.Fueling;
            }
        }
    }
}
