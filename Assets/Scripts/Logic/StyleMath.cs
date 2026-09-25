using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Paint schemes: better looking stations attract more customers.</summary>
    public static class StyleMath
    {
        public const int SchemeCount = 4;

        public static float TrafficFactor(int scheme) => scheme switch
        {
            0 => 0.9f,
            1 => 1f,
            2 => 1.05f,
            3 => 1.1f,
            _ => 1f
        };

        public static float Cost(int scheme) => scheme switch
        {
            1 => 300f,
            2 => 800f,
            3 => 1500f,
            _ => 0f
        };

        public static int RequiredLevel(int scheme) => scheme switch
        {
            2 => 3,
            3 => 5,
            _ => 1
        };

        /// <summary>Main (walls, canopy) and accent (pumps, trims) colors, linear RGB.
        /// Scheme 0 stays near white so the asset pack textures remain readable.</summary>
        public static float3 Primary(int scheme) => scheme switch
        {
            0 => new float3(0.85f, 0.8f, 0.72f),
            1 => new float3(0.95f, 0.95f, 0.92f),
            2 => new float3(0.95f, 0.62f, 0.35f),
            3 => new float3(0.25f, 0.2f, 0.35f),
            _ => new float3(1f, 1f, 1f)
        };

        public static float3 Accent(int scheme) => scheme switch
        {
            0 => new float3(0.78f, 0.64f, 0.54f),
            1 => new float3(0.85f, 0.15f, 0.15f),
            2 => new float3(0.55f, 0.2f, 0.45f),
            3 => new float3(0.1f, 0.9f, 0.85f),
            _ => new float3(1f, 1f, 1f)
        };
    }
}
