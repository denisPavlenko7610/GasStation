using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Pure gameplay formulas, kept separate from systems so they can be unit tested.</summary>
    public static class StationMath
    {
        public const float LostCustomerPenalty = 0.03f;

        /// <summary>Relative traffic for the hour of day: quiet nights, morning and evening rush.</summary>
        public static float TrafficIntensity(float hour)
        {
            return 0.2f
                   + 1.0f * Bump(hour, 8.5f, 2f)
                   + 0.5f * Bump(hour, 13f, 2.5f)
                   + 1.2f * Bump(hour, 18f, 2.2f);
        }

        /// <summary>0.5 at zero reputation, 1.5 at full reputation.</summary>
        public static float ReputationFactor(float reputation) => 0.5f + math.saturate(reputation);

        /// <summary>1 at market price, higher when cheaper, lower when more expensive.</summary>
        public static float PriceAttractiveness(float sellPrice, float marketPrice)
        {
            if (marketPrice <= 0f)
                return 1f;
            float overprice = sellPrice / marketPrice - 1f;
            return math.clamp(1f - overprice * 3f, 0.1f, 2f);
        }

        public static FuelType PickFuelType(float random01)
        {
            if (random01 < 0.5f)
                return FuelType.Petrol92;
            return random01 < 0.85f ? FuelType.Petrol95 : FuelType.Diesel;
        }

        /// <summary>How many liters can be pumped this frame.</summary>
        public static float Dispense(float maxFlow, float remainingRequested, float stock)
        {
            return math.max(0f, math.min(maxFlow, math.min(remainingRequested, stock)));
        }

        public static float Payment(float liters, float price) => liters * price;

        /// <summary>Reputation change after serving a customer.</summary>
        public static float ServiceReputationDelta(float patienceRatio, float sellPrice, float marketPrice)
        {
            float overprice = marketPrice > 0f ? math.max(0f, sellPrice / marketPrice - 1f) : 0f;
            return 0.005f + 0.01f * math.saturate(patienceRatio) - overprice * 0.05f;
        }

        public static float ClampReputation(float reputation) => math.saturate(reputation);

        /// <summary>1 with no litter, 0 when there are dirtyThreshold pieces or more.</summary>
        public static float Cleanliness(int trashCount, int dirtyThreshold)
        {
            if (dirtyThreshold <= 0)
                return 1f;
            return 1f - math.saturate((float)trashCount / dirtyThreshold);
        }

        /// <summary>A dirty station gets fewer customers: 0.6 at a dump, 1.1 when spotless.</summary>
        public static float CleanlinessTrafficFactor(float cleanliness) => math.lerp(0.6f, 1.1f, math.saturate(cleanliness));

        /// <summary>Reputation change per second from how clean the station is.</summary>
        public static float CleanlinessReputationDrift(float cleanliness) => (math.saturate(cleanliness) - 0.6f) * 0.0004f;

        public static float3 QueueSlot(float3 head, float3 direction, float spacing, int index)
        {
            return head + direction * (spacing * index);
        }

        /// <summary>Advances the clock. Returns true when a new day has started.</summary>
        public static bool AdvanceClock(ref float hour, ref int day, float deltaHours)
        {
            hour += deltaHours;
            if (hour < 24f)
                return false;

            hour -= 24f;
            day++;
            return true;
        }

        private static float Bump(float x, float center, float width)
        {
            float d = (x - center) / width;
            return math.exp(-d * d);
        }
    }
}
