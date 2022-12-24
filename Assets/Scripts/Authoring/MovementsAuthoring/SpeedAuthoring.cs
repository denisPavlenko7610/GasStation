using GasStation.Components.MovementComponents;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring.MovementsAuthoring
{
    public class SpeedAuthoring : MonoBehaviour
    {
        [field: SerializeField] public float Value { get; private set; }
    }

    public class SpeedBaker : Baker<SpeedAuthoring>
    {
        public override void Bake(SpeedAuthoring authoring)
        {
            AddComponent(new SpeedComponent
            {
                Value = authoring.Value
            });
        }
    }
}