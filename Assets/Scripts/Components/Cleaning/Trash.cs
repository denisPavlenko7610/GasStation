using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>A piece of litter the player (or a janitor) can pick up.</summary>
    public struct Trash : IComponentData
    {
    }

    /// <summary>Settings for customers dropping litter. Lives on the station entity.</summary>
    public struct TrashSpawner : IComponentData
    {
        /// <summary>Chance per second that one waiting car drops a piece of litter.</summary>
        public float LitterChancePerSecond;
        public int MaxTrash;
        public float PickupRadius;
        public Unity.Mathematics.Random Random;
    }

    public struct TrashPrefabElement : IBufferElementData
    {
        public Entity Prefab;
    }

    public struct StationCleanliness : IComponentData
    {
        /// <summary>0 = dump, 1 = spotless.</summary>
        public float Value;
        public int TrashCount;
        public float JanitorTimer;
    }
}
