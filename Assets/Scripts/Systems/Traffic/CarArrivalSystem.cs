using GasStation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Switches car state when it reaches the end of its path; removes cars that left.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(CarMoveSystem))]
    public partial struct CarArrivalSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (car, path, transform, entity) in SystemAPI
                         .Query<RefRW<Car>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!path.IsEmpty)
                    continue;

                switch (car.ValueRO.State)
                {
                    case CarState.Arriving:
                        car.ValueRW.State = CarState.Queued;
                        break;
                    case CarState.DrivingToPump:
                        car.ValueRW.State = CarState.WaitingForService;
                        if (SystemAPI.Exists(car.ValueRO.Pump))
                            transform.ValueRW.Rotation = SystemAPI.GetComponent<Pump>(car.ValueRO.Pump).StopRotation;
                        break;
                    case CarState.Leaving:
                        ecb.DestroyEntity(entity);
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
