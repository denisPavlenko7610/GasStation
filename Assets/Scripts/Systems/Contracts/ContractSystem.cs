using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Contracts with local companies. Every couple of days a new offer arrives in the laptop mail; accepted
    /// contracts send their vehicles on schedule (as SpawnRequests), pay a fixed price, charge a penalty for
    /// every vehicle that leaves without fuel, break after three misses and pay a bonus when completed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(QuestSystem))]
    public partial struct ContractSystem : ISystem
    {
        private const float VehicleSpacing = 4f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContractBoard>();
            state.RequireForUpdate<Contract>();
            state.RequireForUpdate<ContractOffer>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var contracts = SystemAPI.GetSingletonBuffer<Contract>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            var time = SystemAPI.GetSingleton<GameTime>();

            bool dayEnded = false;
            int count = events.Length;
            for (int i = 0; i < count; i++)
            {
                var e = events[i];
                if (e.Type == StationEventType.DayEnded)
                    dayEnded = true;
                else if (e.Type == StationEventType.ContractVehicleServed)
                    Served(contracts, e.Subject);
                else if (e.Type == StationEventType.ContractVehicleMissed)
                    Missed(contracts, events, ref economy, e.Subject);
            }

            if (dayEnded)
                NewDay(ref state, contracts, events, ref economy);

            SendVehicles(ref state, contracts, time);
        }

        private static void Served(DynamicBuffer<Contract> contracts, int id)
        {
            int index = IndexOf(contracts, id);
            if (index < 0)
                return;
            var contract = contracts[index];
            contract.Served++;
            contracts[index] = contract;
        }

        private static void Missed(DynamicBuffer<Contract> contracts, DynamicBuffer<StationEvent> events, ref Economy economy, int id)
        {
            int index = IndexOf(contracts, id);
            if (index < 0)
                return;

            var contract = contracts[index];
            float penalty = ContractMath.Get(contract.Type).Penalty;
            contract.Missed++;
            economy.Money -= penalty;
            economy.DayExpenses += penalty;
            StationEvent.Push(events, StationEventType.ContractPenalty, default, penalty, id);

            if (!ContractMath.IsBroken(contract.Missed))
            {
                contracts[index] = contract;
                return;
            }

            contracts.RemoveAt(index);
            economy.Reputation = StationMath.ClampReputation(economy.Reputation - ContractMath.CancelReputation);
            StationEvent.Push(events, StationEventType.ContractCancelled, default, (float)contract.Type, id);
        }

        private void NewDay(ref SystemState state, DynamicBuffer<Contract> contracts, DynamicBuffer<StationEvent> events, ref Economy economy)
        {
            for (int i = contracts.Length - 1; i >= 0; i--)
            {
                var contract = contracts[i];
                contract.DaysLeft--;
                if (contract.DaysLeft > 0)
                {
                    contracts[i] = contract;
                    continue;
                }

                float bonus = ContractMath.Get(contract.Type).Bonus;
                economy.Money += bonus;
                economy.DayIncome += bonus;
                economy.Reputation = StationMath.ClampReputation(economy.Reputation + ContractMath.CompleteReputation);
                contracts.RemoveAt(i);
                StationEvent.Push(events, StationEventType.ContractCompleted, default, bonus, contract.Id);
            }

            var offers = SystemAPI.GetSingletonBuffer<ContractOffer>();
            for (int i = offers.Length - 1; i >= 0; i--)
            {
                var offer = offers[i];
                if (--offer.ExpiresIn <= 0)
                    offers.RemoveAt(i);
                else
                    offers[i] = offer;
            }

            ref var board = ref SystemAPI.GetSingletonRW<ContractBoard>().ValueRW;
            if (--board.DaysToNextOffer > 0 || offers.Length >= ContractMath.MaxOffers)
                return;

            board.DaysToNextOffer = ContractMath.DaysBetweenOffers;
            int level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            int type = ContractMath.PickType(board.Random.NextFloat(), level);
            if (type < 0)
                return;

            var contractType = (ContractType)type;
            float market = 1.4f;
            if (SystemAPI.HasSingleton<FuelStock>())
            {
                var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);
                int fuel = (int)ContractMath.Get(contractType).Fuel;
                if (fuel < stock.Length)
                    market = stock[fuel].MarketPrice;
            }

            int id = board.NextId++;
            offers.Add(new ContractOffer
            {
                Id = id,
                Type = contractType,
                Price = ContractMath.OfferPrice(contractType, market * SkillMath.ContractPriceFactor((SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0))),
                Days = ContractMath.Get(contractType).Days,
                ExpiresIn = ContractMath.OfferLifetime
            });
            StationEvent.Push(events, StationEventType.ContractOffered, default, (float)contractType, id);
        }

        private void SendVehicles(ref SystemState state, DynamicBuffer<Contract> contracts, GameTime time)
        {
            if (contracts.Length == 0 || !SystemAPI.HasSingleton<CarSpawner>())
                return;

            var spawnerEntity = SystemAPI.GetSingletonEntity<CarSpawner>();
            if (!SystemAPI.HasBuffer<SpawnRequest>(spawnerEntity))
                return;

            var requests = SystemAPI.GetBuffer<SpawnRequest>(spawnerEntity);
            int hour = (int)time.Hour;
            for (int i = 0; i < contracts.Length; i++)
            {
                var contract = contracts[i];
                if (!ContractMath.IsSlot(contract.Type, hour) || (contract.LastSentDay == time.Day && contract.LastSentHour == hour))
                    continue;

                contract.LastSentDay = time.Day;
                contract.LastSentHour = hour;
                contracts[i] = contract;

                var info = ContractMath.Get(contract.Type);
                for (int v = 0; v < info.VehiclesPerSlot; v++)
                {
                    requests.Add(new SpawnRequest
                    {
                        Customer = CustomerType.Regular,
                        Delay = v == 0 ? 0f : VehicleSpacing,
                        HasHabits = true,
                        Fuel = info.Fuel,
                        LitersMultiplier = info.LitersMultiplier,
                        ContractId = contract.Id
                    });
                }
            }
        }

        private static int IndexOf(DynamicBuffer<Contract> contracts, int id)
        {
            for (int i = 0; i < contracts.Length; i++)
            {
                if (contracts[i].Id == id)
                    return i;
            }

            return -1;
        }
    }
}
