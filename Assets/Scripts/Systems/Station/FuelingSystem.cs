using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Pumps fuel into cars and wears the pump down. When done the customer pays (plus a tip) and leaves;
    /// a thief drives away without paying unless the player stands next to the car.
    /// </summary>
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
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationEvent>();
            state.RequireForUpdate<StationSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>();
            var economy = SystemAPI.GetSingletonRW<Economy>();
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            float flowMultiplier = UpgradeMath.FlowMultiplier(SystemAPI.GetSingleton<StationUpgrades>().PumpSpeed);
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            float catchRadius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            float3 playerPosition = PlayerPosition(ref state, out bool hasPlayer);

            foreach (var (car, patience, path, transform) in SystemAPI
                         .Query<RefRW<Car>, RefRO<Patience>, DynamicBuffer<PathPoint>, RefRO<LocalTransform>>())
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
                float amount = pump.ValueRO.IsBroken
                    ? 0f
                    : StationMath.Dispense(pump.ValueRO.FlowRate * flowMultiplier * deltaTime, remaining, fuel.Amount);
                bool hadFuel = fuel.Amount > 0f;
                fuel.Amount -= amount;
                stock[fuelIndex] = fuel;
                car.ValueRW.ReceivedLiters += amount;

                bool wasWorking = !pump.ValueRO.IsBroken;
                pump.ValueRW.Condition = math.max(0f, pump.ValueRO.Condition - amount * ProgressMath.WearPerLiter);
                if (wasWorking && pump.ValueRO.IsBroken)
                    StationEvent.Push(events, StationEventType.PumpBroken, default, pump.ValueRO.Number);

                bool full = car.ValueRO.ReceivedLiters >= car.ValueRO.RequestedLiters - 0.001f;
                bool outOfFuel = fuel.Amount <= 0f;
                if (hadFuel && outOfFuel)
                    StationEvent.Push(events, StationEventType.FuelRanOut, car.ValueRO.FuelType);
                if (!full && !outOfFuel && !pump.ValueRO.IsBroken)
                    continue;

                ref var eco = ref economy.ValueRW;
                float liters = car.ValueRO.ReceivedLiters;
                float bill = StationMath.Payment(liters, fuel.SellPrice);

                if (liters <= 0f)
                {
                    eco.DayLost++;
                    StationEvent.Push(events, StationEventType.CustomerLeftAngry, car.ValueRO.FuelType);
                    eco.Reputation = StationMath.ClampReputation(eco.Reputation - StationMath.LostCustomerPenalty);
                }
                else if (car.ValueRO.Customer == CustomerType.Thief &&
                         !(hasPlayer && math.distancesq(playerPosition.xz, transform.ValueRO.Position.xz) <= catchRadius * catchRadius))
                {
                    StationEvent.Push(events, StationEventType.FuelStolen, car.ValueRO.FuelType, bill);
                }
                else
                {
                    float patienceRatio = patience.ValueRO.Max > 0f ? patience.ValueRO.Current / patience.ValueRO.Max : 0f;
                    float tip = CustomerProfiles.Tip(car.ValueRO.Customer, bill, patienceRatio);
                    eco.Money += bill + tip;
                    eco.DayIncome += bill + tip;
                    eco.DayServed++;
                    eco.Reputation = StationMath.ClampReputation(
                        eco.Reputation + StationMath.ServiceReputationDelta(patienceRatio, fuel.SellPrice, fuel.MarketPrice));

                    StationEvent.Push(events, StationEventType.CustomerPaid, car.ValueRO.FuelType, bill);
                    if (tip >= 0.5f)
                        StationEvent.Push(events, StationEventType.TipReceived, default, tip);
                    if (car.ValueRO.Customer == CustomerType.Thief)
                        StationEvent.Push(events, StationEventType.ThiefCaught, default, bill);
                }

                pump.ValueRW.Occupant = Entity.Null;
                CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
            }
        }

        private float3 PlayerPosition(ref SystemState state, out bool found)
        {
            found = false;
            float3 position = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                position = transform.ValueRO.Position;
                found = true;
            }

            return position;
        }
    }
}
