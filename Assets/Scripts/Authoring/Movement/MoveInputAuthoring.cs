using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    public class MoveInputAuthoring : MonoBehaviour
    {
    }

    public class MoveInputBaker : Baker<MoveInputAuthoring>
    {
        public override void Bake(MoveInputAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MoveInput());
        }
    }
}
