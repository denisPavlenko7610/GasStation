using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Turns this frame's station events into experience and levels.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(QuestSystem))]
    public partial struct StationLevelSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationLevel>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            float experience = 0f;
            for (int i = 0; i < events.Length; i++)
                experience += ProgressMath.Experience(events[i].Type);

            if (experience <= 0f)
                return;

            var level = SystemAPI.GetSingletonRW<StationLevel>();
            int gained = ProgressMath.AddExperience(ref level.ValueRW, experience);
            if (gained > 0)
                StationEvent.Push(events, StationEventType.LevelUp, default, level.ValueRO.Level);
        }
    }
}
