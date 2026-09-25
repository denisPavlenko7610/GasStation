using GasStation.Components;

namespace GasStation.Logic
{
    public static class RenovationMath
    {
        public const float InteractionRadius = 3.5f;

        public static float Cost(RenovationKind kind) => kind switch
        {
            RenovationKind.Windows => 150f,
            RenovationKind.Graffiti => 120f,
            RenovationKind.Fence => 250f,
            RenovationKind.Lamps => 300f,
            RenovationKind.VendingMachine => 250f,
            RenovationKind.Sign => 400f,
            _ => 200f
        };

        public static int RequiredLevel(RenovationKind kind) => kind switch
        {
            RenovationKind.Lamps => 2,
            RenovationKind.VendingMachine => 2,
            RenovationKind.Sign => 2,
            _ => 1
        };

        /// <summary>Extra traffic once fixed: a tidy station attracts more drivers.</summary>
        public static float TrafficBonus(RenovationKind kind) => kind switch
        {
            RenovationKind.Sign => 0.06f,
            RenovationKind.Lamps => 0.04f,
            RenovationKind.Windows => 0.03f,
            RenovationKind.Graffiti => 0.03f,
            _ => 0.02f
        };

        public const float ReputationBonus = 0.03f;
    }
}
