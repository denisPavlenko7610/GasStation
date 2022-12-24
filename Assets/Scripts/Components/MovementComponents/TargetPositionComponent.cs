using Unity.Entities;
using UnityEngine;

namespace GasStation.Components.MovementComponents
{
    public struct TargetPositionComponent : IComponentData
    {
        public Vector3 Value;
    }
}