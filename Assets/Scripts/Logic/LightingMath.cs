using Unity.Mathematics;

namespace GasStation.Logic
{
    public static class LightingMath
    {
        public const float DuskStart = 18.5f;
        public const float DuskEnd = 19.5f;
        public const float DawnStart = 5.5f;
        public const float DawnEnd = 6.5f;

        /// <summary>0 in daylight, 1 at night, smooth over half an hour around dusk and dawn.</summary>
        public static float NightFactor(float hour)
        {
            hour = hour - 24f * math.floor(hour / 24f);
            if (hour >= DuskEnd || hour < DawnStart)
                return 1f;
            if (hour >= DuskStart)
                return math.smoothstep(DuskStart, DuskEnd, hour);
            if (hour < DawnEnd)
                return 1f - math.smoothstep(DawnStart, DawnEnd, hour);
            return 0f;
        }
    }
}
