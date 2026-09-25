using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Tire service bay. Works after the TireService upgrade: cars that need tires drive here after
    /// the pump; the player presses E next to the bay (or a mechanic starts the job).
    /// </summary>
    public class TireServiceAuthoring : MonoBehaviour
    {
        public Transform[] entryRoute;
        [Tooltip("Seconds per job at upgrade level 1")] public float duration = 8f;
        [Tooltip("Price at upgrade level 1")] public float price = 25f;
    }

    public class TireServiceBaker : Baker<TireServiceAuthoring>
    {
        public override void Bake(TireServiceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var transform = GetComponent<Transform>();
            AddComponent(entity, new TireService
            {
                Bay = transform.position,
                BayRotation = transform.rotation,
                Duration = authoring.duration,
                Price = authoring.price,
                Occupant = Entity.Null
            });

            var entry = AddBuffer<TireEntryPoint>(entity);
            if (authoring.entryRoute == null)
                return;

            foreach (var point in authoring.entryRoute)
            {
                if (point == null)
                    continue;
                DependsOn(point);
                entry.Add(new TireEntryPoint { Position = point.position });
            }
        }
    }
}
