using GasStation.Components;
using Unity.Burst;
using Unity.Collections;
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
            // Snapshot of every car position so the job can keep clear of the car ahead.
            var positions = new NativeList<float3>(64, Allocator.TempJob);
            foreach (var transform in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<Car>())
                positions.Add(transform.ValueRO.Position);

            new CarMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Positions = positions.AsDeferredJobArray()
            }.ScheduleParallel();
            // ScheduleParallel() merges into state.Dependency; dispose the snapshot after it runs.
            state.Dependency = positions.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Drives each car towards the first point of its path; removes the point on arrival.
    /// Cars brake smoothly into their stop points and hold a clear gap behind the car ahead.
    /// </summary>
    [BurstCompile]
    public partial struct CarMoveJob : IJobEntity
    {
        private const float ArrivalDistance = 0.05f;
        /// <summary>How far ahead of the nose a blocking check looks.</summary>
        private const float KeepClearAhead = 4.5f;
        /// <summary>Another car within this radius of the look-ahead point stops us.</summary>
        private const float StopRadiusSq = 2.4f * 2.4f;
        /// <summary>A car this close overlaps us (spawn stack-up) and does not count as a blocker.</summary>
        private const float OverlapRadiusSq = 1.5f * 1.5f;
        /// <summary>Grace period after which a stuck car pushes through (spawn stacks, rare deadlocks).</summary>
        private const float BlockBreakSeconds = 2.5f;
        /// <summary>Distance from the target where braking starts.</summary>
        private const float BrakingDistance = 6f;
        private const float MinBrakeFactor = 0.35f;

        public float DeltaTime;
        [ReadOnly] public NativeArray<float3> Positions;

        private void Execute(ref LocalTransform transform, ref Car car, ref DynamicBuffer<PathPoint> path, in CarMovement movement)
        {
            if (path.IsEmpty)
                return;

            float3 target = path[0].Position;
            target.y = transform.Position.y;

            float3 toTarget = target - transform.Position;
            float distance = math.length(toTarget);

            // Ease off the throttle as the stop point approaches.
            float speed = movement.Speed * math.clamp(distance / BrakingDistance, MinBrakeFactor, 1f);
            float step = speed * DeltaTime;

            if (distance <= math.max(step, ArrivalDistance))
            {
                transform.Position = target;
                path.RemoveAt(0);
                car.BlockedTimer = 0f;
                return;
            }

            float3 direction = toTarget / distance;

            // Hold position while a car stands right in front of the nose. Overlapping cars
            // (spawn stack-up) do not block, and long stalls push through after a grace period.
            float3 ahead = transform.Position + direction * KeepClearAhead;
            bool blocked = false;
            for (int i = 0; i < Positions.Length; i++)
            {
                if (math.distancesq(Positions[i], ahead) < StopRadiusSq &&
                    math.distancesq(Positions[i], transform.Position) > OverlapRadiusSq)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
            {
                car.BlockedTimer += DeltaTime;
                if (car.BlockedTimer < BlockBreakSeconds)
                    return;
            }
            else
            {
                car.BlockedTimer = 0f;
            }

            transform.Position += direction * step;
            transform.Rotation = math.slerp(
                transform.Rotation,
                quaternion.LookRotationSafe(direction, math.up()),
                math.saturate(movement.TurnSpeed * DeltaTime));
        }
    }
}
