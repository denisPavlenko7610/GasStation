using GasStation.Components.MovementComponents;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring.MovementsAuthoring
{
    public class TargetPositionAuthoring : MonoBehaviour
    {
        [field: SerializeField] public Vector3 TargetPosition { get; private set; }
    }
    
    public class TargetPositionBaker : Baker<TargetPositionAuthoring>
    {
        public override void Bake(TargetPositionAuthoring authoring)
        {
            AddComponent(new TargetPositionComponent
            {
                Value = authoring.TargetPosition
            });
        }
    }
}