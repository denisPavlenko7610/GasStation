using Unity.Entities;

namespace GasStation.Components
{
    public struct PlayerInteraction : IComponentData
    {
        public bool InteractPressed;
        /// <summary>True while E (or Fire) is held; hold-to-pump fueling uses it.</summary>
        public bool InteractHeld;
        public Entity NearbyPump;
        public Entity NearbyTrash;
        public Entity NearbyRenovation;
    }
}
