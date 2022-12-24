using Unity.Entities;
using UnityEngine;

namespace GasStation.Components.StandardComponents
{
    public struct RigidbodyComponent : IComponentData
    {
        public Rigidbody Value;
    }
}