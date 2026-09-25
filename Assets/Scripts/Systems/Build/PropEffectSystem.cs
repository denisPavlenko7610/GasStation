using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Recounts placed props for the systems that use them (traffic, patience, night crime) and earns the
    /// small extra income of air pumps and water machines from this frame's paying customers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(FinanceSystem))]
    public partial struct PropEffectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PropEffects>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var effects = ref SystemAPI.GetSingletonRW<PropEffects>().ValueRW;
            effects.ClearCounts();
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
                effects.Add(prop.ValueRO.Type);

            if (effects.AirPumps == 0 && effects.WaterMachines == 0)
                return;

            var events = SystemAPI.GetSingletonBuffer<StationEvent>(true);
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            float airChance = PropMath.AirPumpChance(effects.AirPumps);
            float waterChance = PropMath.WaterMachineChance(effects.WaterMachines);

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type != StationEventType.CustomerPaid)
                    continue;

                if (effects.Random.NextFloat() < airChance)
                {
                    economy.Money += PropMath.AirPumpIncome;
                    economy.DayIncome += PropMath.AirPumpIncome;
                    economy.Reputation = StationMath.ClampReputation(economy.Reputation + PropMath.AirPumpReputation);
                }

                if (effects.Random.NextFloat() < waterChance)
                {
                    economy.Money += PropMath.WaterMachineIncome;
                    economy.DayIncome += PropMath.WaterMachineIncome;
                }
            }
        }
    }
}
