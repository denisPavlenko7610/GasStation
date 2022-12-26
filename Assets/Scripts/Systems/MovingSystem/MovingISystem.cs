using GasStation.Aspects.MovementAspects;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems.MovingSystem
{
    public partial struct MovingISystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var moveToPositionAspect in SystemAPI.Query<MoveToPositionAspect>())
            {
                moveToPositionAspect.Move(SystemAPI.Time.DeltaTime);
            }
        }
    }
}