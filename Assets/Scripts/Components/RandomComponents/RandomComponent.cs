using Unity.Entities;

namespace GasStation.Components.RandomComponents
{
    public struct RandomComponent : IComponentData
    {
        public Unity.Mathematics.Random Value;
    }
}