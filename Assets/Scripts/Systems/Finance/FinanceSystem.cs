using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>
    /// Utility bills every night; tax, loan payment and insurance premium every week; insurance payouts for
    /// thefts; bankruptcy when the station stays in the red for too long.
    /// Reads this frame's events, so it runs after every system that pushes them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(QuestSystem))]
    public partial struct FinanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Finance>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var finance = ref SystemAPI.GetSingletonRW<Finance>().ValueRW;
            if (finance.Bankrupt)
                return;

            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;
            var difficulty = SystemAPI.HasSingleton<StationRules>() ? SystemAPI.GetSingleton<StationRules>().Difficulty : Difficulty.Normal;

            bool dayEnded = false;
            float payout = 0f;
            int count = events.Length;
            for (int i = 0; i < count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case StationEventType.CustomerPaid:
                        finance.ElectricityToday += e.Value * FinanceMath.ElectricityPerFuelDollar;
                        break;
                    case StationEventType.CarWashed:
                        finance.ElectricityToday += FinanceMath.ElectricityPerWash;
                        finance.WaterToday += FinanceMath.WaterPerWash;
                        break;
                    case StationEventType.RestroomUsed:
                        finance.WaterToday += FinanceMath.WaterPerRestroomVisit;
                        break;
                    case StationEventType.FuelStolen:
                    case StationEventType.WorkerStole:
                        if (finance.Insured)
                            payout += e.Value * FinanceMath.InsuranceCoverage;
                        break;
                    case StationEventType.DayEnded:
                        dayEnded = true;
                        break;
                }
            }

            if (payout > 0f)
            {
                payout = math.round(payout);
                economy.Money += payout;
                economy.DayIncome += payout;
                StationEvent.Push(events, StationEventType.InsurancePayout, default, payout);
            }

            if (!dayEnded)
                return;

            float multiplier = FinanceMath.BillMultiplier(difficulty);
            int pumps = SystemAPI.QueryBuilder().WithAll<Pump>().Build().CalculateEntityCount();

            float utilities = math.round(
                (finance.ElectricityToday + FinanceMath.DailyElectricity(pumps) + finance.WaterToday) * multiplier);
            finance.ElectricityToday = 0f;
            finance.WaterToday = 0f;
            Charge(ref economy, utilities);
            StationEvent.Push(events, StationEventType.UtilitiesPaid, default, utilities);

            if (SystemAPI.HasSingleton<DayReport>())
                finance.WeekRevenue += SystemAPI.GetSingleton<DayReport>().Income;

            int day = SystemAPI.GetSingleton<GameTime>().Day;
            if (FinanceMath.IsWeekStart(day))
                WeeklyBills(ref finance, ref economy, events, multiplier);

            UpdateBankruptcy(ref finance, economy.Money, difficulty, events);
        }

        private static void WeeklyBills(ref Finance finance, ref Economy economy, DynamicBuffer<StationEvent> events, float multiplier)
        {
            float tax = FinanceMath.WeeklyTax(finance.WeekRevenue);
            finance.WeekRevenue = 0f;
            if (tax > 0f)
            {
                Charge(ref economy, tax);
                StationEvent.Push(events, StationEventType.TaxPaid, default, tax);
            }

            if (finance.Insured)
            {
                float premium = math.round(FinanceMath.InsurancePremium * multiplier);
                Charge(ref economy, premium);
                StationEvent.Push(events, StationEventType.InsurancePremiumPaid, default, premium);
            }

            if (finance.Loan == LoanKind.None || finance.LoanBalance <= 0f)
                return;

            float payment = finance.WeeklyPayment < finance.LoanBalance ? finance.WeeklyPayment : finance.LoanBalance;
            finance.LoanBalance -= payment;
            Charge(ref economy, payment);
            StationEvent.Push(events, StationEventType.LoanPayment, default, payment);

            if (finance.LoanBalance <= 0.01f)
            {
                finance.Loan = LoanKind.None;
                finance.LoanBalance = 0f;
                finance.WeeklyPayment = 0f;
                StationEvent.Push(events, StationEventType.LoanRepaid);
            }
        }

        private static void UpdateBankruptcy(ref Finance finance, float money, Difficulty difficulty, DynamicBuffer<StationEvent> events)
        {
            if (money >= 0f)
            {
                finance.DaysInDebt = 0;
                return;
            }

            finance.DaysInDebt++;
            int limit = FinanceMath.BankruptcyDays(difficulty);
            if (limit <= 0)
                return;

            if (finance.DaysInDebt >= limit)
            {
                finance.Bankrupt = true;
                StationEvent.Push(events, StationEventType.GameOver, default, finance.DaysInDebt);
            }
            else if (finance.DaysInDebt >= FinanceMath.BankruptcyWarningDays)
            {
                StationEvent.Push(events, StationEventType.BankruptcyWarning, default, limit - finance.DaysInDebt);
            }
        }

        private static void Charge(ref Economy economy, float amount)
        {
            economy.Money -= amount;
            economy.DayExpenses += amount;
        }
    }
}
