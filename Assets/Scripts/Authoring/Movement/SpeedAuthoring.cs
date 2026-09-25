using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    public class SpeedAuthoring : MonoBehaviour
    {
        [field: SerializeField] public float Value { get; private set; } = 5f;
    }

    public class SpeedBaker : Baker<SpeedAuthoring>
    {
        public override void Bake(SpeedAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MoveSpeed { Value = authoring.Value });
        }
    }
}
