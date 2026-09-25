using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>All gameplay systems. Runs before transforms are updated.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class StationSystemGroup : ComponentSystemGroup
    {
    }
}
