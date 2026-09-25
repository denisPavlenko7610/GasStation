using GasStation.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Cars appear at this transform, drive through entryRoute, wait in the queue that starts at queueHead
    /// and grows along -queueHead.forward, then leave through exitRoute (the last point despawns the car).
    /// </summary>
    public class CarSpawnerAuthoring : MonoBehaviour
    {
        public GameObject[] carPrefabs;
        public Transform[] entryRoute;
        public Transform queueHead;
        public float queueSpacing = 6f;
        public Transform[] exitRoute;

        [Header("Traffic")]
        [Tooltip("Seconds between cars at normal traffic")] public float baseInterval = 10f;
        public int maxCars = 12;
        public Vector2 speedRange = new(6f, 9f);
        [Tooltip("Seconds a customer waits before leaving")] public Vector2 patienceRange = new(45f, 90f);
        public Vector2 litersRange = new(15f, 50f);
        public uint seed = 1;
    }

    public class CarSpawnerBaker : Baker<CarSpawnerAuthoring>
    {
        public override void Bake(CarSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var transform = GetComponent<Transform>();

            float3 queueHead = transform.position;
            float3 queueDirection = -transform.forward;
            if (authoring.queueHead != null)
            {
                DependsOn(authoring.queueHead);
                queueHead = authoring.queueHead.position;
                queueDirection = -authoring.queueHead.forward;
            }

            AddComponent(entity, new CarSpawner
            {
                SpawnPosition = transform.position,
                SpawnRotation = transform.rotation,
                QueueHead = queueHead,
                QueueDirection = math.normalizesafe(new float3(queueDirection.x, 0f, queueDirection.z)),
                QueueSpacing = authoring.queueSpacing,
                BaseInterval = math.max(0.5f, authoring.baseInterval),
                Timer = 1f,
                MaxCars = authoring.maxCars,
                SpeedRange = authoring.speedRange,
                PatienceRange = authoring.patienceRange,
                LitersRange = authoring.litersRange,
                NextArrivalOrder = 0,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var prefabs = AddBuffer<CarPrefabElement>(entity);
            if (authoring.carPrefabs != null)
            {
                foreach (var prefab in authoring.carPrefabs)
                {
                    if (prefab != null)
                        prefabs.Add(new CarPrefabElement { Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }

            var entry = AddBuffer<EntryRoutePoint>(entity);
            foreach (var point in Valid(authoring.entryRoute))
                entry.Add(new EntryRoutePoint { Position = point.position });

            AddBuffer<SpawnRequest>(entity);

            var exit = AddBuffer<ExitRoutePoint>(entity);
            foreach (var point in Valid(authoring.exitRoute))
                exit.Add(new ExitRoutePoint { Position = point.position });
        }

        private System.Collections.Generic.IEnumerable<Transform> Valid(Transform[] points)
        {
            if (points == null)
                yield break;

            foreach (var point in points)
            {
                if (point == null)
                    continue;
                DependsOn(point);
                yield return point;
            }
        }
    }
}
