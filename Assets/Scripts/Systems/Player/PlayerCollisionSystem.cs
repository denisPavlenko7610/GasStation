using GasStation.Components;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace GasStation.Systems
{
    /// <summary>
    /// Keeps the first-person owner out of walls, fences and props: after the move, the body
    /// capsule is pushed horizontally out of every scenery collider it overlaps. The scenery is plain GameObjects
    /// with PhysX colliders, so this uses the classic physics queries.
    /// </summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerMoveSystem))]
    [UpdateBefore(typeof(PlayerInteractionSystem))]
    public partial class PlayerCollisionSystem : SystemBase
    {
        private const float Radius = 0.3f;
        // The entity origin is 1 m above the ground; the capsule starts above ankle height so the ground and
        // curbs don't count as walls.
        private const float Bottom = -0.55f;
        private const float Top = 0.8f;
        private const int Iterations = 3;

        private readonly Collider[] _hits = new Collider[16];
        private CapsuleCollider _probe;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnDestroy()
        {
            if (_probe != null)
                Object.Destroy(_probe.gameObject);
        }

        protected override void OnUpdate()
        {
            var probe = Probe();
            foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<PlayerTag>())
            {
                Vector3 position = transform.ValueRO.Position;
                for (int iteration = 0; iteration < Iterations; iteration++)
                {
                    probe.transform.position = position;
                    int count = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (Bottom + Radius),
                        position + Vector3.up * (Top - Radius), Radius, _hits, ~0, QueryTriggerInteraction.Ignore);

                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    {
                        var other = _hits[i];
                        if (other == probe)
                            continue;

                        if (!Physics.ComputePenetration(probe, position, Quaternion.identity, other,
                                other.transform.position, other.transform.rotation, out var direction, out float distance))
                            continue;

                        var push = new Vector3(direction.x, 0f, direction.z);
                        if (push.sqrMagnitude < 0.01f)
                            continue;

                        position += push.normalized * distance;
                        moved = true;
                    }

                    if (!moved)
                        break;
                }

                transform.ValueRW.Position = position;
            }
        }

        private CapsuleCollider Probe()
        {
            if (_probe != null)
                return _probe;

            var go = new GameObject("PlayerCollisionProbe") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            _probe = go.AddComponent<CapsuleCollider>();
            _probe.isTrigger = true;
            _probe.radius = Radius;
            _probe.height = Top - Bottom;
            _probe.center = new Vector3(0f, (Top + Bottom) * 0.5f, 0f);
            return _probe;
        }
    }
}
