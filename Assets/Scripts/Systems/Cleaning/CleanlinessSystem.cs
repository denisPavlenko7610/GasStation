using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Measures how clean the station is, lets janitors pick up litter and drifts reputation.</summary>
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
            var cleanliness = SystemAPI.GetSingletonRW<StationCleanliness>();
            float janitors = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>().Janitor : 0f;

            if (janitors > 0f && !_trash.IsEmpty)
            {
                cleanliness.ValueRW.JanitorTimer += deltaTime;
                if (cleanliness.ValueRO.JanitorTimer >= StaffMath.JanitorInterval(janitors))
                {
                    cleanliness.ValueRW.JanitorTimer = 0f;
                    using var trash = _trash.ToEntityArray(Allocator.Temp);
                    // Janitor work counts for cleanliness only, not for the player's cleanup quests.
                    state.EntityManager.DestroyEntity(trash[0]);
                    cleanliness = SystemAPI.GetSingletonRW<StationCleanliness>();
                }
            }

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
