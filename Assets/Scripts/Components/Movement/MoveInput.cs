using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    /// <summary>Normalized movement direction requested by the player (XZ plane).</summary>
    public struct MoveInput : IComponentData
    {
        public float3 Value;
    }
}
