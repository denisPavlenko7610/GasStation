using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Put at the restroom door. Shop visitors use it; the player cleans it with E.</summary>
    public class RestroomAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float startDirt = 0.5f;
        [Range(0f, 1f)] public float dirtPerVisit = 0.08f;
        [Range(0f, 1f)] public float visitChance = 0.4f;
    }

    public class RestroomBaker : Baker<RestroomAuthoring>
    {
        public override void Bake(RestroomAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Restroom
            {
                Door = GetComponent<Transform>().position,
                Dirt = authoring.startDirt,
                DirtPerVisit = authoring.dirtPerVisit,
                VisitChance = authoring.visitChance
            });
        }
    }
}
