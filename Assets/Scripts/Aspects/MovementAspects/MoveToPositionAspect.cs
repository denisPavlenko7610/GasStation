using GasStation.Components.MovementComponents;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Aspects.MovementAspects
{
    public readonly partial struct MoveToPositionAspect : IAspect
    {
        private readonly TransformAspect _transformAspect;
        private readonly RefRO<SpeedComponent> _speed;
        private readonly RefRO<TargetPositionComponent> _targetPosition;

        [BurstCompile]
        public void Move(float deltaTime)
        {
            _transformAspect.WorldPosition += (float3)_targetPosition.ValueRO.Value * deltaTime * _speed.ValueRO.Value;
        }
    }
}