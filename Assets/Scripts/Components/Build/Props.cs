using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    /// <summary>Objects the player places in build mode. Each one has a gameplay effect (see PropMath).</summary>
    public enum PropType : byte
    {
        TrashBin = 0,
        Bench = 1,
        Planter = 2,
        Lamp = 3,
        RoadSign = 4,
        AirPump = 5,
        WaterMachine = 6,
        SecurityCamera = 7
    }

    public static class PropTypes
    {
        public const int Count = 8;
    }

    /// <summary>
    /// A placed object. Pure data: the Mono PropPresenter draws it, systems read its type and position.
    /// </summary>
    public struct PlacedProp : IComponentData
    {
        public int Id;
        public PropType Type;
        public float3 Position;
        /// <summary>Degrees around Y.</summary>
        public float Yaw;
    }

    /// <summary>Where props may stand: the lot rectangle minus the no-build zones. Lives on the station entity.</summary>
    public struct BuildArea : IComponentData
    {
        public float2 Min;
        public float2 Max;
        public int NextPropId;
    }

    /// <summary>Lanes, pump islands and buildings: nothing can be placed inside.</summary>
    [InternalBufferCapacity(0)]
    public struct NoBuildZone : IBufferElementData
    {
        public float2 Min;
        public float2 Max;
    }

    /// <summary>How many props of each type stand on the lot, recounted every frame by PropEffectSystem.</summary>
    public struct PropEffects : IComponentData
    {
        public int TrashBins;
        public int Benches;
        public int Planters;
        public int Lamps;
        public int RoadSigns;
        public int AirPumps;
        public int WaterMachines;
        public int Cameras;
        public Random Random;

        public int Get(PropType type) => type switch
        {
            PropType.TrashBin => TrashBins,
            PropType.Bench => Benches,
            PropType.Planter => Planters,
            PropType.Lamp => Lamps,
            PropType.RoadSign => RoadSigns,
            PropType.AirPump => AirPumps,
            PropType.WaterMachine => WaterMachines,
            _ => Cameras
        };

        public void Add(PropType type)
        {
            switch (type)
            {
                case PropType.TrashBin: TrashBins++; break;
                case PropType.Bench: Benches++; break;
                case PropType.Planter: Planters++; break;
                case PropType.Lamp: Lamps++; break;
                case PropType.RoadSign: RoadSigns++; break;
                case PropType.AirPump: AirPumps++; break;
                case PropType.WaterMachine: WaterMachines++; break;
                default: Cameras++; break;
            }
        }

        public void ClearCounts()
        {
            TrashBins = Benches = Planters = Lamps = RoadSigns = AirPumps = WaterMachines = Cameras = 0;
        }
    }
}
