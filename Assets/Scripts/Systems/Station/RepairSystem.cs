using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>
    /// Each interact press next to a worn pump repairs a quarter of it for a fee; the press that lifts it over
    /// the threshold finishes the job. Hired mechanics walk over and service worn pumps (StaffAgentSystem).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    [UpdateAfter(typeof(TireServiceSystem))]
    [UpdateBefore(typeof(TrashPickupSystem))]
    public partial struct RepairSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var economy = SystemAPI.GetSingletonRW<Economy>();
            int skills = (SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0);
            float stepCost = ProgressMath.RepairStepCost * SkillMath.RepairCostFactor(skills);
            float repairStep = ProgressMath.RepairStep * SkillMath.RepairStepFactor(skills);

            foreach (var interaction in SystemAPI.Query<RefRW<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (!interaction.ValueRO.InteractPressed || pumpEntity == Entity.Null || !SystemAPI.Exists(pumpEntity))
                    continue;

                var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                if (pump.ValueRO.Condition >= ProgressMath.RepairThreshold)
                    continue;

                // E at a pump with a customer means fueling (start or resume holding), never a paid repair.
                var occupant = pump.ValueRO.Occupant;
                if (occupant != Entity.Null && SystemAPI.Exists(occupant) &&
                    SystemAPI.GetComponent<Car>(occupant).State is CarState.WaitingForService or CarState.Fueling)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                if (economy.ValueRO.Money < stepCost)
                {
                    StationEvent.Push(events, StationEventType.NotEnoughMoney, default, stepCost);
                    continue;
                }

                economy.ValueRW.Money -= stepCost;
                economy.ValueRW.DayExpenses += stepCost;
                // The step that lifts the pump over the threshold finishes the repair.
                float step = pump.ValueRO.Condition + repairStep >= ProgressMath.RepairThreshold
                    ? 1f
                    : repairStep;
                Repair(ref pump.ValueRW, step, events);
            }

            // Mechanics walk to worn pumps and service them (StaffAgentSystem).
        }

        private static void Repair(ref Pump pump, float amount, DynamicBuffer<StationEvent> events)
        {
            bool wasWorn = pump.Condition < 1f;
            pump.Condition = math.min(1f, pump.Condition + amount);
            if (wasWorn && pump.Condition >= 1f)
                StationEvent.Push(events, StationEventType.PumpRepaired, default, pump.Number);
        }
    }
}
