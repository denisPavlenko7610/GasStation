using GasStation.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PatienceSystem))]
    public partial struct CarMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CarMoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }
    }

    /// <summary>Drives each car towards the first point of its path; removes the point on arrival.</summary>
    [BurstCompile]
    public partial struct CarMoveJob : IJobEntity
    {
        private const float ArrivalDistance = 0.05f;

        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref DynamicBuffer<PathPoint> path, in CarMovement movement)
        {
            if (path.IsEmpty)
                return;

            float3 target = path[0].Position;
            target.y = transform.Position.y;

            float3 toTarget = target - transform.Position;
            float distance = math.length(toTarget);
            float step = movement.Speed * DeltaTime;

            if (distance <= math.max(step, ArrivalDistance))
            {
                transform.Position = target;
                path.RemoveAt(0);
                return;
            }

            float3 direction = toTarget / distance;
            transform.Position += direction * step;
            transform.Rotation = math.slerp(
                transform.Rotation,
                quaternion.LookRotationSafe(direction, math.up()),
                math.saturate(movement.TurnSpeed * DeltaTime));
        }
    }
}
