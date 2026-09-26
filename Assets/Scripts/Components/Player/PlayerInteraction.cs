using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public struct PlayerInteraction : IComponentData
    {
        public bool InteractPressed;
        /// <summary>True while E (or Fire) is held; hold-to-pump fueling uses it.</summary>
        public bool InteractHeld;
        /// <summary>Where the first-person camera looks (unit vector); targets in view win over ones behind.</summary>
        public float3 LookDirection;
        public Entity NearbyPump;
        public Entity NearbyTrash;
        public Entity NearbyRenovation;
    }
}
