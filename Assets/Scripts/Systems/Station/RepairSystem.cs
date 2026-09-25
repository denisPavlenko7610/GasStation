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

            foreach (var interaction in SystemAPI.Query<RefRW<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (!interaction.ValueRO.InteractPressed || pumpEntity == Entity.Null || !SystemAPI.Exists(pumpEntity))
                    continue;

                var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                if (pump.ValueRO.Condition >= ProgressMath.RepairThreshold)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                if (economy.ValueRO.Money < ProgressMath.RepairStepCost)
                {
                    StationEvent.Push(events, StationEventType.NotEnoughMoney, default, ProgressMath.RepairStepCost);
                    continue;
                }

                economy.ValueRW.Money -= ProgressMath.RepairStepCost;
                economy.ValueRW.DayExpenses += ProgressMath.RepairStepCost;
                // The step that lifts the pump over the threshold finishes the repair.
                float step = pump.ValueRO.Condition + ProgressMath.RepairStep >= ProgressMath.RepairThreshold
                    ? 1f
                    : ProgressMath.RepairStep;
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
