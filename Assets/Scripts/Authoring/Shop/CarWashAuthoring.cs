using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Wash bay. Works after the CarWash upgrade: cars drive from the pump through entryRoute to this
    /// transform, get washed, then leave through exitRoute (the last point despawns the car).
    /// </summary>
    public class CarWashAuthoring : MonoBehaviour
    {
        public Transform[] entryRoute;
        public Transform[] exitRoute;
        [Tooltip("Seconds per wash at upgrade level 1")] public float duration = 12f;
        [Tooltip("Price at upgrade level 1")] public float price = 8f;
    }

    public class CarWashBaker : Baker<CarWashAuthoring>
    {
        public override void Bake(CarWashAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var transform = GetComponent<Transform>();
            AddComponent(entity, new CarWash
            {
                Bay = transform.position,
                BayRotation = transform.rotation,
                Duration = authoring.duration,
                Price = authoring.price,
                Occupant = Entity.Null
            });

            var entry = AddBuffer<WashEntryPoint>(entity);
            if (authoring.entryRoute != null)
            {
                foreach (var point in authoring.entryRoute)
                {
                    if (point == null)
                        continue;
                    DependsOn(point);
                    entry.Add(new WashEntryPoint { Position = point.position });
                }
            }

            var exit = AddBuffer<WashExitPoint>(entity);
            if (authoring.exitRoute != null)
            {
                foreach (var point in authoring.exitRoute)
                {
                    if (point == null)
                        continue;
                    DependsOn(point);
                    exit.Add(new WashExitPoint { Position = point.position });
                }
            }
        }
    }
}
