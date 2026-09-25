using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInputSystem))]
    public partial struct PlayerMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int skills = SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0;
            new PlayerMoveJob { DeltaTime = SystemAPI.Time.DeltaTime, SpeedFactor = SkillMath.SpeedFactor(skills) }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithAll(typeof(PlayerTag))]
    public partial struct PlayerMoveJob : IJobEntity
    {
        public float DeltaTime;
        /// <summary>The owner's Runner skill.</summary>
        public float SpeedFactor;

        private void Execute(ref LocalTransform transform, in MoveInput input, in MoveSpeed speed)
        {
            transform.Position += input.Value * speed.Value * SpeedFactor * DeltaTime;
        }
    }
}
