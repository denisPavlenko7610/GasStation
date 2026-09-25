using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public struct Pump : IComponentData
    {
        public int Number;
        public float3 InteractionPoint;
        public float3 StopPosition;
        public quaternion StopRotation;
        /// <summary>Liters per second.</summary>
        public float FlowRate;
        public Entity Occupant;
        /// <summary>ExtraPump upgrade level needed before cars use this pump. 0 = always open.</summary>
        public int RequiredUpgradeLevel;
        /// <summary>1 = new, 0 = broken. Wears down with every liter pumped.</summary>
        public float Condition;

        public bool IsBroken => Condition <= 0f;
    }
}
