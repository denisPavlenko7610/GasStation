using Unity.Entities;

namespace GasStation.Components
{
    public enum Difficulty : byte
    {
        /// <summary>Half bills, no bankruptcy.</summary>
        Relaxed = 0,
        Normal = 1,
        /// <summary>Higher bills, bankruptcy comes fast.</summary>
        Survival = 2
    }

    /// <summary>Rules chosen for this game (new game dialog).</summary>
    public struct StationRules : IComponentData
    {
        public Difficulty Difficulty;
    }

    public enum LoanKind : byte
    {
        None = 0,
        Small = 1,
        Large = 2
    }

    /// <summary>Bank, bills, taxes and insurance. Lives on the station entity.</summary>
    public struct Finance : IComponentData
    {
        public LoanKind Loan;
        /// <summary>What is still owed, interest included.</summary>
        public float LoanBalance;
        public float WeeklyPayment;

        public bool Insured;

        /// <summary>Utilities used today, billed at midnight.</summary>
        public float ElectricityToday;
        public float WaterToday;

        /// <summary>Income since the last weekly tax.</summary>
        public float WeekRevenue;

        public int DaysInDebt;
        public bool Bankrupt;
    }

    public enum CompetitorPromo : byte
    {
        None = 0,
        Discount = 1,
        Advertising = 2
    }

    /// <summary>The rival station across the road.</summary>
    public struct Competitor : IComponentData
    {
        public bool Active;
        public bool BoughtOut;
        public int OpensOnDay;
        public float Petrol92;
        public float Petrol95;
        public float Diesel;
        /// <summary>0..1</summary>
        public float Reputation;
        public CompetitorPromo Promo;
        public int PromoDaysLeft;
        /// <summary>Our share of the drivers who stop at either station, 0..1.</summary>
        public float OurShare;
        public Unity.Mathematics.Random Random;

        public float Price(FuelType type) => type switch
        {
            FuelType.Petrol92 => Petrol92,
            FuelType.Petrol95 => Petrol95,
            _ => Diesel
        };

        public void SetPrice(FuelType type, float price)
        {
            switch (type)
            {
                case FuelType.Petrol92: Petrol92 = price; break;
                case FuelType.Petrol95: Petrol95 = price; break;
                default: Diesel = price; break;
            }
        }
    }
}
