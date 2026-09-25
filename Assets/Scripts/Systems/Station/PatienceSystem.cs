using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Customers who wait too long leave without paying and hurt reputation.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(FuelingSystem))]
    public partial struct PatienceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var economy = SystemAPI.GetSingletonRW<Economy>();
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());

            foreach (var (car, patience, path) in SystemAPI
                         .Query<RefRW<Car>, RefRW<Patience>, DynamicBuffer<PathPoint>>())
            {
                var carState = car.ValueRO.State;
                if (carState != CarState.Queued && carState != CarState.WaitingForService)
                    continue;

                patience.ValueRW.Current -= deltaTime;
                if (patience.ValueRO.Current > 0f)
                    continue;

                var pumpEntity = car.ValueRO.Pump;
                if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                    SystemAPI.GetComponentRW<Pump>(pumpEntity).ValueRW.Occupant = Entity.Null;

                economy.ValueRW.DayLost++;
                StationEvent.Push(events, StationEventType.CustomerLeftAngry, car.ValueRO.FuelType);
                StationEvent.Push(events, StationEventType.CustomerReview, default, ReviewMath.AngryStars);
                VisitOutcome.Rate(events, car.ValueRO, ReviewMath.AngryStars);
                economy.ValueRW.Reputation = StationMath.ClampReputation(
                    economy.ValueRO.Reputation - StationMath.LostCustomerPenalty);

                CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
            }
        }
    }
}
