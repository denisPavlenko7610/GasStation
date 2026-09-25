using System;
using GasStation.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Build mode area: the lot rectangle where props may stand and the zones that stay free (lanes, pump
    /// islands, buildings). Shown as gizmos: green = lot, red = no-build zones.
    /// </summary>
    public class BuildAreaAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            public Vector2 min;
            public Vector2 max;

            public Zone(float minX, float minZ, float maxX, float maxZ)
            {
                min = new Vector2(minX, minZ);
                max = new Vector2(maxX, maxZ);
            }
        }

        [Tooltip("World X/Z of the lot corners")]
        public Vector2 lotMin = new(-36f, -15f);
        public Vector2 lotMax = new(36f, 27f);
        public Zone[] noBuildZones = Array.Empty<Zone>();
        public uint seed = 5;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
            DrawRect(lotMin, lotMax);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            foreach (var zone in noBuildZones)
                DrawRect(zone.min, zone.max);
        }

        private static void DrawRect(Vector2 min, Vector2 max)
        {
            var center = new Vector3((min.x + max.x) / 2f, 0.05f, (min.y + max.y) / 2f);
            Gizmos.DrawWireCube(center, new Vector3(max.x - min.x, 0.1f, max.y - min.y));
        }
    }

    public class BuildAreaBaker : Baker<BuildAreaAuthoring>
    {
        public override void Bake(BuildAreaAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BuildArea
            {
                Min = authoring.lotMin,
                Max = authoring.lotMax,
                NextPropId = 1
            });
            AddComponent(entity, new PropEffects { Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed) });

            var zones = AddBuffer<NoBuildZone>(entity);
            foreach (var zone in authoring.noBuildZones)
            {
                zones.Add(new NoBuildZone
                {
                    Min = math.min((float2)zone.min, zone.max),
                    Max = math.max((float2)zone.min, zone.max)
                });
            }
        }
    }
}
