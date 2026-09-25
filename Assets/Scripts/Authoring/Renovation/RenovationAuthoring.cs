using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>Put in the SubScene where the player should stand to fix something. Pair it with a RenovationVisual.</summary>
    public class RenovationAuthoring : MonoBehaviour
    {
        [Tooltip("0..63, the same id as the RenovationVisual in the main scene")]
        public int id;
        public RenovationKind kind;
        public bool startDone;
    }

    public class RenovationBaker : Baker<RenovationAuthoring>
    {
        public override void Bake(RenovationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Renovation
            {
                Id = Mathf.Clamp(authoring.id, 0, 63),
                Kind = authoring.kind,
                Position = GetComponent<Transform>().position,
                Done = authoring.startDone
            });
        }
    }
}
