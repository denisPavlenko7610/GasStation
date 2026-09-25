using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Measures how clean the station is and drifts reputation.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(LitterSystem))]
    public partial struct CleanlinessSystem : ISystem
    {
        private EntityQuery _trash;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationCleanliness>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
            _trash = SystemAPI.QueryBuilder().WithAll<Trash>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            // Janitors pick litter up themselves now (StaffAgentSystem).
            var cleanliness = SystemAPI.GetSingletonRW<StationCleanliness>();

            int count = _trash.CalculateEntityCount();
            int threshold = SystemAPI.GetSingleton<StationSettings>().DirtyThreshold;
            float value = StationMath.Cleanliness(count, threshold);
            if (SystemAPI.HasSingleton<Restroom>())
                value = FacilityMath.CombinedCleanliness(value, SystemAPI.GetSingleton<Restroom>().Dirt);
            cleanliness.ValueRW.TrashCount = count;
            cleanliness.ValueRW.Value = value;

            var economy = SystemAPI.GetSingletonRW<Economy>();
            economy.ValueRW.Reputation = StationMath.ClampReputation(
                economy.ValueRO.Reputation + StationMath.CleanlinessReputationDrift(value) * deltaTime);
        }
    }
}
