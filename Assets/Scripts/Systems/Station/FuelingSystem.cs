using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Pumps fuel into cars, takes payment and sends them away.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    public partial struct FuelingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<FuelStock>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>();
            var economy = SystemAPI.GetSingletonRW<Economy>();
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());

            foreach (var (car, patience, path) in SystemAPI
                         .Query<RefRW<Car>, RefRO<Patience>, DynamicBuffer<PathPoint>>())
            {
                if (car.ValueRO.State != CarState.Fueling)
                    continue;

                var pumpEntity = car.ValueRO.Pump;
                if (pumpEntity == Entity.Null || !SystemAPI.Exists(pumpEntity))
                {
                    CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
                    continue;
                }

                var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                int fuelIndex = (int)car.ValueRO.FuelType;
                var fuel = stock[fuelIndex];

                float remaining = car.ValueRO.RequestedLiters - car.ValueRO.ReceivedLiters;
                float amount = StationMath.Dispense(pump.ValueRO.FlowRate * deltaTime, remaining, fuel.Amount);
                fuel.Amount -= amount;
                stock[fuelIndex] = fuel;
                car.ValueRW.ReceivedLiters += amount;

                bool full = car.ValueRO.ReceivedLiters >= car.ValueRO.RequestedLiters - 0.001f;
                bool outOfFuel = fuel.Amount <= 0f;
                if (!full && !outOfFuel)
                    continue;

                ref var eco = ref economy.ValueRW;
                if (car.ValueRO.ReceivedLiters > 0f)
                {
                    float payment = StationMath.Payment(car.ValueRO.ReceivedLiters, fuel.SellPrice);
                    float patienceRatio = patience.ValueRO.Max > 0f ? patience.ValueRO.Current / patience.ValueRO.Max : 0f;
                    eco.Money += payment;
                    eco.DayIncome += payment;
                    eco.DayServed++;
                    eco.Reputation = StationMath.ClampReputation(
                        eco.Reputation + StationMath.ServiceReputationDelta(patienceRatio, fuel.SellPrice, fuel.MarketPrice));
                }
                else
                {
                    eco.DayLost++;
                    eco.Reputation = StationMath.ClampReputation(eco.Reputation - StationMath.LostCustomerPenalty);
                }

                pump.ValueRW.Occupant = Entity.Null;
                CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
            }
        }
    }
}
