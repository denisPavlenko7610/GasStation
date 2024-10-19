using GasStation.Components.MovementComponents;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Aspects.MovementAspects
{
    [BurstCompile]
    public readonly partial struct MoveToPositionAspect : IAspect
    {
        private readonly RefRW<LocalTransform> _localTransform; // Use LocalTransform instead of TransformAspect
        private readonly RefRO<SpeedComponent> _speed;
        private readonly RefRO<TargetPositionComponent> _targetPosition;

        public void Move(float deltaTime)
        {
            _localTransform.ValueRW.Position += (float3)_targetPosition.ValueRO.Value * deltaTime * _speed.ValueRO.Value;
        }
    }
}