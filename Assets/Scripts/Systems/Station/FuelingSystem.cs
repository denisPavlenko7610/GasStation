using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Pumps fuel into cars and wears the pump down. When done the customer pays (plus a tip) and goes
    /// shopping or gets ready to leave; a thief drives away without paying unless the player stands next to the car.
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
            bool hasShop = SystemAPI.HasSingleton<Shop>();
            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>() ? SystemAPI.GetSingleton<StationCleanliness>().Value : 1f;
            bool hasRegulars = SystemAPI.HasSingleton<RegularState>();
            var regulars = hasRegulars ? SystemAPI.GetSingletonBuffer<RegularState>(true) : default;
            bool hasContracts = SystemAPI.HasSingleton<Contract>();
            var contracts = hasContracts ? SystemAPI.GetSingletonBuffer<Contract>(true) : default;

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
                bool paid = false;
                // Contract vehicles pay the price fixed in the contract.
                float price = hasContracts && car.ValueRO.ContractId > 0
                    ? ContractPrice(contracts, car.ValueRO.ContractId, fuel.SellPrice)
                    : fuel.SellPrice;
                float bill = StationMath.Payment(liters, price);

                if (liters <= 0f)
                {
                    eco.DayLost++;
                    StationEvent.Push(events, StationEventType.CustomerLeftAngry, car.ValueRO.FuelType);
                    StationEvent.Push(events, StationEventType.CustomerReview, default, ReviewMath.AngryStars);
                    VisitOutcome.Rate(events, car.ValueRO, ReviewMath.AngryStars);
                    VisitOutcome.ContractResult(events, car.ValueRO, false);
                    eco.Reputation = StationMath.ClampReputation(eco.Reputation - StationMath.LostCustomerPenalty);
                }
                else if (car.ValueRO.Customer == CustomerType.Thief &&
                         !(hasPlayer && math.distancesq(playerPosition.xz, transform.ValueRO.Position.xz) <= catchRadius * catchRadius) &&
                         !CaughtOnCamera(ref state, transform.ValueRO.Position))
                {
                    StationEvent.Push(events, StationEventType.FuelStolen, car.ValueRO.FuelType, bill);
                }
                else
                {
                    float patienceRatio = patience.ValueRO.Max > 0f ? patience.ValueRO.Current / patience.ValueRO.Max : 0f;
                    float tip = CustomerProfiles.Tip(car.ValueRO.Customer, bill, patienceRatio);
                    int regularIndex = car.ValueRO.RegularId - 1;
                    if (hasRegulars && regularIndex >= 0 && regularIndex < regulars.Length)
                        tip += bill * VisitorMath.TipShare(regulars[regularIndex].Loyalty);
                    eco.Money += bill + tip;
                    eco.DayIncome += bill + tip;
                    eco.DayServed++;
                    eco.Reputation = StationMath.ClampReputation(
                        eco.Reputation + StationMath.ServiceReputationDelta(patienceRatio, fuel.SellPrice, fuel.MarketPrice));

                    StationEvent.Push(events, StationEventType.CustomerPaid, car.ValueRO.FuelType, bill);
                    VisitOutcome.ContractResult(events, car.ValueRO, true);
                    if (car.ValueRO.Customer != CustomerType.Thief)
                    {
                        float stars = ReviewMath.Stars(patienceRatio, fuel.SellPrice, fuel.MarketPrice, cleanliness);
                        StationEvent.Push(events, StationEventType.CustomerReview, default, stars);
                        VisitOutcome.Rate(events, car.ValueRO, stars);
                    }

                    if (car.ValueRO.Customer == CustomerType.Emergency)
                    {
                        eco.Reputation = StationMath.ClampReputation(eco.Reputation + VisitorMath.EmergencyReputation);
                        StationEvent.Push(events, StationEventType.EmergencyServed);
                    }

                    if (tip >= 0.5f)
                        StationEvent.Push(events, StationEventType.TipReceived, default, tip);
                    if (car.ValueRO.Customer == CustomerType.Thief)
                        StationEvent.Push(events, StationEventType.ThiefCaught, default, bill);
                    paid = true;
                }

                if (paid)
                {
                    // The car keeps the pump while the driver shops; CarWashSystem frees it on departure.
                    car.ValueRW.State = car.ValueRO.WantsShop && hasShop ? CarState.Shopping : CarState.ReadyToLeave;
                    continue;
                }

                pump.ValueRW.Occupant = Entity.Null;
                CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
            }
        }

        private static float ContractPrice(DynamicBuffer<Contract> contracts, int id, float fallback)
        {
            for (int i = 0; i < contracts.Length; i++)
            {
                if (contracts[i].Id == id)
                    return contracts[i].Price;
            }

            return fallback;
        }

        /// <summary>Security cameras near the pump may catch a thief the player did not stop.</summary>
        private bool CaughtOnCamera(ref SystemState state, float3 position)
        {
            if (!SystemAPI.HasSingleton<PropEffects>())
                return false;

            float radius = PropMath.Get(PropType.SecurityCamera).Radius;
            int near = 0;
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
            {
                if (prop.ValueRO.Type == PropType.SecurityCamera &&
                    math.distancesq(prop.ValueRO.Position.xz, position.xz) <= radius * radius)
                    near++;
            }

            if (near == 0)
                return false;

            ref var effects = ref SystemAPI.GetSingletonRW<PropEffects>().ValueRW;
            return effects.Random.NextFloat() < PropMath.CameraCatchChance(near);
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
