using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>
    /// Picks what the first-person owner means to interact with: the closest target in reach, with targets
    /// the camera looks at preferred over ones beside or behind.
    /// </summary>
    public static class InteractionMath
    {
        /// <summary>Within this distance facing doesn't matter (the thing is at your feet).</summary>
        public const float AtFeet = 0.8f;

        /// <summary>
        /// Lower is better; float.MaxValue when the target is out of reach. The distance is weighted by up to
        /// ×4 for a target straight behind the view.
        /// </summary>
        public static float Score(float2 player, float2 target, float3 look, float radius)
        {
            float2 offset = target - player;
            float distanceSq = math.lengthsq(offset);
            if (distanceSq > radius * radius)
                return float.MaxValue;
            if (distanceSq < AtFeet * AtFeet)
                return distanceSq;

            float2 view = math.normalizesafe(look.xz);
            if (math.lengthsq(view) < 0.5f)
                return distanceSq;

            float facing = math.dot(offset * math.rsqrt(distanceSq), view);
            return distanceSq * (1f + (1f - facing) * 1.5f);
        }
    }
}
