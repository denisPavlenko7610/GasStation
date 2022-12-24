using GasStation.Components.MovementComponents;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Aspects.MovementAspects
{
    public readonly partial struct MoveToPositionAspect : IAspect
    {
        private readonly Entity _entity;
        private readonly TransformAspect _transformAspect;
        private readonly RefRO<SpeedComponent> _speed;
        private readonly RefRW<TargetPositionComponent> _targetPosition;

        public void Move(float deltaTime)
        {
            float3 direction = math.normalize(_targetPosition.ValueRW.Value - (Vector3)_transformAspect.WorldPosition);
            _transformAspect.WorldPosition += direction * deltaTime * _speed.ValueRO.Value;
        }
    }
}