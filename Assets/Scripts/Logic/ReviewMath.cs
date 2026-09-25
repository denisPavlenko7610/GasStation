using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>How a customer rates the visit, 1..5 stars.</summary>
    public static class ReviewMath
    {
        public const int VariantsPerStar = 3;

        /// <summary>
        /// Starts from 5 stars: waiting too long, paying above the market price and a dirty station take stars away.
        /// </summary>
        public static int Stars(float patienceRatio, float sellPrice, float marketPrice, float cleanliness)
        {
            float stars = 5f;
            stars -= (1f - math.saturate(patienceRatio)) * 2f;
            if (marketPrice > 0f)
                stars -= math.saturate((sellPrice / marketPrice - 1f) * 5f) * 1.5f;
            stars -= (1f - math.saturate(cleanliness)) * 1.5f;
            return (int)math.clamp(math.round(stars), 1f, 5f);
        }

        /// <summary>Customers who leave without being served always give one star.</summary>
        public const int AngryStars = 1;
    }
}
