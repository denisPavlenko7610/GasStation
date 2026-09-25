using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Hired attendants start fueling cars that have been waiting at a pump long enough.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    [UpdateBefore(typeof(FuelingSystem))]
    public partial struct AttendantSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float delay = UpgradeMath.AttendantDelay(SystemAPI.GetSingleton<StationUpgrades>().Attendant);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            foreach (var car in SystemAPI.Query<RefRW<Car>>())
            {
                if (car.ValueRO.State != CarState.WaitingForService)
                    continue;

                car.ValueRW.ServiceWait += deltaTime;
                if (car.ValueRO.ServiceWait < delay)
                    continue;

                car.ValueRW.State = CarState.Fueling;
                StationEvent.Push(events, StationEventType.FuelingStarted, car.ValueRO.FuelType);
            }
        }
    }
}
