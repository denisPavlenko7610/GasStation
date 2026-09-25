using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>
    /// Sums the working power of hired staff per role every frame: only people on shift count, scaled by
    /// skill, energy, mood and trait. Energy drains on shift and recovers off it. Every morning workers gain
    /// experience, their mood follows pay and tiredness, unhappy ones quit, dishonest ones may steal from the
    /// till, and a new set of applicants arrives.
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
            state.RequireForUpdate<GameTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var stationEntity = SystemAPI.GetSingletonEntity<StaffRoster>();
            ref var roster = ref SystemAPI.GetComponentRW<StaffRoster>(stationEntity).ValueRW;
            var candidates = SystemAPI.GetBuffer<StaffCandidate>(stationEntity);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            bool newDay = StationEvent.Contains(events, StationEventType.DayEnded);

            var time = SystemAPI.GetSingleton<GameTime>();
            float drainFactor = SkillMath.StaffDrainFactor((SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0));
            float deltaHours = SystemAPI.Time.DeltaTime * time.MinutesPerSecond / 60f;
            foreach (var worker in SystemAPI.Query<RefRW<Worker>>())
            {
                ref var w = ref worker.ValueRW;
                if (StaffMath.OnShift(w.Shift, time.Hour))
                {
                    w.Energy = math.max(0f, w.Energy - StaffMath.EnergyDrain(w.Trait) * drainFactor * deltaHours);
                    if (w.Energy <= 0f)
                        w.Exhausted = true;
                }
                else
                {
                    w.Energy = math.min(1f, w.Energy + StaffMath.EnergyRecoverPerHour * deltaHours);
                }
            }

            if (newDay)
            {
                ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (worker, entity) in SystemAPI.Query<RefRW<Worker>>().WithEntityAccess())
                {
                    worker.ValueRW.DaysWorked++;
                    worker.ValueRW.Skill = StaffMath.GrowSkill(worker.ValueRO.Skill);

                    ref var w = ref worker.ValueRW;
                    w.Mood = StaffMath.NextMood(w.Mood, w.Wage, StaffMath.Wage(w.Role, w.Skill), w.Exhausted);
                    w.Exhausted = false;
                    w.UnhappyDays = w.Mood < StaffMath.UnhappyMood ? w.UnhappyDays + 1 : 0;
                    if (w.UnhappyDays >= StaffMath.UnhappyDaysToQuit)
                    {
                        StationEvent.Push(events, StationEventType.WorkerQuit, default, (float)w.Role, w.NameIndex);
                        ecb.DestroyEntity(entity);
                        continue;
                    }

                    if (!StaffMath.Steals(worker.ValueRO.Honesty, roster.Random.NextFloat()))
                        continue;

                    float stolen = worker.ValueRO.Wage * roster.Random.NextFloat(0.3f, 1f);
                    economy.Money -= stolen;
                    economy.DayExpenses += stolen;
                    StationEvent.Push(events, StationEventType.WorkerStole, default, stolen);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                // Structural changes above: fetch the station data again.
                roster = ref SystemAPI.GetComponentRW<StaffRoster>(stationEntity).ValueRW;
                candidates = SystemAPI.GetBuffer<StaffCandidate>(stationEntity);
                events = SystemAPI.GetSingletonBuffer<StationEvent>();
                candidates.Clear();
            }

            // Cooks only apply once there is a diner to cook in.
            bool hasDiner = SystemAPI.HasSingleton<StationUpgrades>() && SystemAPI.GetSingleton<StationUpgrades>().Diner > 0;
            while (candidates.Length < StaffMath.CandidatesPerDay)
            {
                var candidate = StaffMath.Generate(ref roster.Random);
                if (candidate.Role == StaffRole.Cook && !hasDiner)
                {
                    candidate.Role = (StaffRole)roster.Random.NextInt((int)StaffRole.Cook);
                    candidate.Wage = StaffMath.Wage(candidate.Role, candidate.Skill);
                }

                candidates.Add(candidate);
            }

            var power = new StaffPower();
            foreach (var worker in SystemAPI.Query<RefRO<Worker>>())
            {
                float skill = StaffMath.Efficiency(worker.ValueRO, time.Hour);
                switch (worker.ValueRO.Role)
                {
                    case StaffRole.Attendant: power.Attendant += skill; break;
                    case StaffRole.Janitor: power.Janitor += skill; break;
                    case StaffRole.Mechanic: power.Mechanic += skill; break;
                    case StaffRole.Cashier: power.Cashier += skill; break;
                    case StaffRole.Cook: power.Cook += skill; break;
                }

                power.Headcount++;
            }

            SystemAPI.SetComponent(stationEntity, power);
        }
    }
}
