using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>Current quest. Index walks through QuestCatalog: story quests first, then endless daily ones.</summary>
    public struct QuestProgress : IComponentData
    {
        public int Index;
        /// <summary>Accumulated progress for counter goals (trash, customers, orders, upgrades).</summary>
        public float Counter;
        public int Completed;
    }
}
