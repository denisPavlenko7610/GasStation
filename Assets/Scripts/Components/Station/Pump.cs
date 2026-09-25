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
    }
}
