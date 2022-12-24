using GasStation.Components.PlayerComponents;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring.PlayerAuthoring
{
    public class PlayerTagAuthoring : MonoBehaviour
    {
        
    }
    
    public class PlayerTagBaker : Baker<PlayerTagAuthoring>
    {
        public override void Bake(PlayerTagAuthoring authoring)
        {
            AddComponent(new PlayerTag());
        }
    }
}