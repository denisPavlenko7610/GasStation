using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public enum RenovationKind : byte
    {
        Windows = 0,
        Graffiti = 1,
        Fence = 2,
        Lamps = 3,
        VendingMachine = 4,
        Sign = 5
    }

    /// <summary>
    /// Something broken on the abandoned station that the player fixes for money (E next to it).
    /// The "before/after" look is done by RenovationVisual objects in the scene with the same Id.
    /// </summary>
    public struct Renovation : IComponentData
    {
        /// <summary>0..63, unique per station; matches RenovationVisual.id.</summary>
        public int Id;
        public RenovationKind Kind;
        public float3 Position;
        public bool Done;
    }
}
