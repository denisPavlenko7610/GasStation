using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Loans, bills, taxes, insurance and bankruptcy.</summary>
    public static class FinanceMath
    {
        public const int DaysPerWeek = 7;
        public const float TaxRate = 0.1f;
        public const float InsurancePremium = 60f;
        public const float InsuranceCoverage = 0.8f;

        public const float BaseElectricityPerDay = 15f;
        /// <summary>Pumps, card terminals and the canopy: per dollar of fuel sold.</summary>
        public const float ElectricityPerFuelDollar = 0.01f;
        public const float ElectricityPerWash = 0.8f;
        /// <summary>Canopy light over one pump for one night.</summary>
        public const float ElectricityPerNightLight = 5f;
        public const float WaterPerWash = 1.5f;
        public const float WaterPerRestroomVisit = 0.4f;

        public static float LoanAmount(LoanKind kind) => kind switch
        {
            LoanKind.Small => 5000f,
            LoanKind.Large => 20000f,
            _ => 0f
        };

        /// <summary>Total interest over the whole loan.</summary>
        public static float LoanInterest(LoanKind kind) => kind switch
        {
            LoanKind.Small => 0.1f,
            LoanKind.Large => 0.18f,
            _ => 0f
        };

        public static int LoanWeeks(LoanKind kind) => kind switch
        {
            LoanKind.Small => 4,
            LoanKind.Large => 8,
            _ => 1
        };

        public static float LoanTotal(LoanKind kind) => math.round(LoanAmount(kind) * (1f + LoanInterest(kind)));

        public static float LoanWeeklyPayment(LoanKind kind) => math.ceil(LoanTotal(kind) / LoanWeeks(kind));

        /// <summary>Paying early saves half of the interest that is still left.</summary>
        public static float EarlyRepayment(float balance, LoanKind kind)
        {
            float interestShare = LoanInterest(kind) / (1f + LoanInterest(kind));
            return math.ceil(balance * (1f - interestShare * 0.5f));
        }

        public static float BillMultiplier(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Relaxed => 0.5f,
            Difficulty.Survival => 1.5f,
            _ => 1f
        };

        /// <summary>Days in the red before the game is over; 0 = never.</summary>
        public static int BankruptcyDays(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Relaxed => 0,
            Difficulty.Survival => 4,
            _ => 7
        };

        /// <summary>Warning after this many days in the red.</summary>
        public const int BankruptcyWarningDays = 3;

        public static float DailyElectricity(int pumps) => BaseElectricityPerDay + pumps * ElectricityPerNightLight;

        public static bool IsWeekStart(int day) => day > 1 && (day - 1) % DaysPerWeek == 0;

        public static float WeeklyTax(float revenue) => math.max(0f, math.round(revenue * TaxRate));
    }

    public enum CompetitorMove : byte
    {
        Hold,
        CutPrices,
        RaisePrices,
        Promo
    }

    /// <summary>How drivers choose between our station and the competitor.</summary>
    public static class CompetitionMath
    {
        public const int OpensOnDay = 3;
        public const float BuyoutPrice = 50000f;
        public const int BuyoutLevel = 8;
        public const float DiscountShare = 0.9f;

        /// <summary>How attractive a station is: price against the market, reputation and extras.</summary>
        public static float Appeal(float averagePrice, float averageMarket, float reputation, float extras)
        {
            float price = StationMath.PriceAttractiveness(averagePrice, averageMarket);
            return price * (0.5f + math.saturate(reputation)) * math.max(0.1f, extras);
        }

        public static float Share(float ourAppeal, float theirAppeal)
        {
            float total = ourAppeal + theirAppeal;
            return total > 0f ? ourAppeal / total : 1f;
        }

        /// <summary>Traffic multiplier for us: 1 at an even split, up to 1.6 when we win everyone.</summary>
        public static float TrafficFactor(float share) => math.clamp(share * 2f, 0.3f, 1.6f);

        /// <summary>Their reaction each morning, from how much of the road we took yesterday.</summary>
        public static CompetitorMove DecideMove(float ourShare, float random01)
        {
            if (ourShare > 0.6f)
                return random01 < 0.35f ? CompetitorMove.Promo : CompetitorMove.CutPrices;
            if (ourShare > 0.5f)
                return random01 < 0.5f ? CompetitorMove.CutPrices : CompetitorMove.Hold;
            if (ourShare < 0.35f)
                return CompetitorMove.RaisePrices;
            return CompetitorMove.Hold;
        }

        /// <summary>
        /// Next price of one fuel: follow the market, then move 4% down or up. Never below 90% or above 115% of
        /// the market price.
        /// </summary>
        public static float NextPrice(float current, float market, CompetitorMove move)
        {
            float target = math.lerp(current, market, 0.5f);
            if (move == CompetitorMove.CutPrices)
                target *= 0.96f;
            else if (move == CompetitorMove.RaisePrices)
                target *= 1.04f;
            return math.round(math.clamp(target, market * 0.9f, market * 1.15f) * 100f) / 100f;
        }

        /// <summary>Their reputation slowly settles at 0.6; a promo raises it a little.</summary>
        public static float NextReputation(float reputation, CompetitorPromo promo) =>
            math.saturate(math.lerp(reputation, 0.6f, 0.1f) + (promo != CompetitorPromo.None ? 0.01f : 0f));

        /// <summary>Services and looks raise how attractive our station is: 1 with nothing, up to about 1.7.</summary>
        public static float OurExtras(float cleanlinessFactor, int services, float decorFactor) =>
            cleanlinessFactor * (1f + 0.05f * services) * decorFactor;

        public static bool CanBuyOut(int stationLevel, float money) => stationLevel >= BuyoutLevel && money >= BuyoutPrice;

        public static float PromoFactor(CompetitorPromo promo) => promo switch
        {
            CompetitorPromo.Discount => 1.15f,
            CompetitorPromo.Advertising => 1.25f,
            _ => 1f
        };
    }
}
