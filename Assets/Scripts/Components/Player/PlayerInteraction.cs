using Unity.Entities;

namespace GasStation.Components
{
    public struct PlayerInteraction : IComponentData
    {
        public bool InteractPressed;
        public Entity NearbyPump;
        public Entity NearbyTrash;
        public Entity NearbyRenovation;
    }
}
