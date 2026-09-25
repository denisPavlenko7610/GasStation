using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>Popularity level of the station. Experience comes from customers, cleanup, repairs and quests.</summary>
    public struct StationLevel : IComponentData
    {
        public int Level;
        public float Experience;
    }

    public enum WorldEventKind : byte
    {
        None,
        RushHour,
        Sandstorm
    }

    /// <summary>Market, random events and their random state. Lives on the station entity.</summary>
    public struct WorldEvents : IComponentData
    {
        public WorldEventKind Active;
        /// <summary>Game hours left for the active event.</summary>
        public float HoursLeft;
        /// <summary>Whole game hour last checked for a new event.</summary>
        public int LastRolledHour;
        public int LastRobberyDay;
        public Unity.Mathematics.Random Random;
    }
}
