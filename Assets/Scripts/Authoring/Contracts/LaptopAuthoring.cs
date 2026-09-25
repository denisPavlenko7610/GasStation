using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>The office laptop: the player walks up to it and presses E to open mail, bank and statistics.</summary>
    public class LaptopAuthoring : MonoBehaviour
    {
    }

    public class LaptopBaker : Baker<LaptopAuthoring>
    {
        public override void Bake(LaptopAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Laptop { Position = authoring.transform.position });
        }
    }
}
