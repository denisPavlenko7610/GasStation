using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Put on the root of a litter object placed in the SubScene.</summary>
    public class TrashAuthoring : MonoBehaviour
    {
    }

    public class TrashBaker : Baker<TrashAuthoring>
    {
        public override void Bake(TrashAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Trash());
        }
    }
}
