using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Put next to StationAuthoring. Customers drop these prefabs as litter.</summary>
    public class TrashSpawnerAuthoring : MonoBehaviour
    {
        public GameObject[] trashPrefabs;
        [Tooltip("Chance per second that one waiting car drops litter")]
        public float litterChancePerSecond = 0.02f;
        [Tooltip("No new litter appears above this amount")]
        public int maxTrash = 80;
        public float pickupRadius = 2.5f;
        public uint seed = 7;
    }

    public class TrashSpawnerBaker : Baker<TrashSpawnerAuthoring>
    {
        public override void Bake(TrashSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TrashSpawner
            {
                LitterChancePerSecond = authoring.litterChancePerSecond,
                MaxTrash = authoring.maxTrash,
                PickupRadius = authoring.pickupRadius,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var prefabs = AddBuffer<TrashPrefabElement>(entity);
            if (authoring.trashPrefabs == null)
                return;

            foreach (var prefab in authoring.trashPrefabs)
            {
                if (prefab != null)
                    prefabs.Add(new TrashPrefabElement { Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
            }
        }
    }
}
