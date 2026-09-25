using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Sums the skill of hired staff per role every frame. Every morning workers gain experience,
    /// dishonest ones may steal from the till, and a new set of applicants arrives.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    [UpdateBefore(typeof(StationCommandSystem))]
    public partial struct StaffSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StaffRoster>();
            state.RequireForUpdate<StaffPower>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var stationEntity = SystemAPI.GetSingletonEntity<StaffRoster>();
            ref var roster = ref SystemAPI.GetComponentRW<StaffRoster>(stationEntity).ValueRW;
            var candidates = SystemAPI.GetBuffer<StaffCandidate>(stationEntity);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            bool newDay = false;
            for (int i = 0; i < events.Length; i++)
                newDay |= events[i].Type == StationEventType.DayEnded;

            if (newDay)
            {
                ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
                foreach (var worker in SystemAPI.Query<RefRW<Worker>>())
                {
                    worker.ValueRW.DaysWorked++;
                    worker.ValueRW.Skill = StaffMath.GrowSkill(worker.ValueRO.Skill);

                    if (!StaffMath.Steals(worker.ValueRO.Honesty, roster.Random.NextFloat()))
                        continue;

                    float stolen = worker.ValueRO.Wage * roster.Random.NextFloat(0.3f, 1f);
                    economy.Money -= stolen;
                    economy.DayExpenses += stolen;
                    StationEvent.Push(events, StationEventType.WorkerStole, default, stolen);
                }

                candidates.Clear();
            }

            while (candidates.Length < StaffMath.CandidatesPerDay)
                candidates.Add(StaffMath.Generate(ref roster.Random));

            var power = new StaffPower();
            foreach (var worker in SystemAPI.Query<RefRO<Worker>>())
            {
                float skill = worker.ValueRO.Skill;
                switch (worker.ValueRO.Role)
                {
                    case StaffRole.Attendant: power.Attendant += skill; break;
                    case StaffRole.Janitor: power.Janitor += skill; break;
                    case StaffRole.Mechanic: power.Mechanic += skill; break;
                    case StaffRole.Cashier: power.Cashier += skill; break;
                }

                power.Headcount++;
            }

            SystemAPI.SetComponent(stationEntity, power);
        }
    }
}
