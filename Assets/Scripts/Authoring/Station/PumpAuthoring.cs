using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Place on a fuel dispenser. The car parks at stopPoint, facing its forward.</summary>
    public class PumpAuthoring : MonoBehaviour
    {
        public int number = 1;
        public Transform stopPoint;
        [Tooltip("Liters per second")] public float flowRate = 5f;
    }

    public class PumpBaker : Baker<PumpAuthoring>
    {
        public override void Bake(PumpAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var transform = GetComponent<Transform>();

            Vector3 stopPosition = transform.position + transform.right * 2f;
            Quaternion stopRotation = transform.rotation;
            if (authoring.stopPoint != null)
            {
                DependsOn(authoring.stopPoint);
                stopPosition = authoring.stopPoint.position;
                stopRotation = authoring.stopPoint.rotation;
            }

            AddComponent(entity, new Pump
            {
                Number = authoring.number,
                InteractionPoint = transform.position,
                StopPosition = stopPosition,
                StopRotation = stopRotation,
                FlowRate = authoring.flowRate,
                Occupant = Entity.Null
            });
        }
    }
}
