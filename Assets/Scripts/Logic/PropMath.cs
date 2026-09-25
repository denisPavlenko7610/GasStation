using System.Collections.Generic;
using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public enum PlacementError : byte
    {
        None,
        OutsideLot,
        Blocked,
        TooClose,
        TooMany,
        NeedsLevel,
        NoMoney
    }

    public struct PropInfo
    {
        public float Cost;
        public int RequiredLevel;
        /// <summary>Radius of the local effect; 0 = the effect works for the whole station.</summary>
        public float Radius;
    }

    /// <summary>Build mode: prices, placement rules and what every prop does.</summary>
    public static class PropMath
    {
        public const int MaxProps = 60;
        public const float MinSpacing = 1.5f;
        public const float GridStep = 0.5f;
        public const float RefundShare = 0.5f;
        /// <summary>Right-click removes the nearest prop within this distance.</summary>
        public const float PickRadius = 1.5f;

        public const float BinLitterFactor = 0.3f;
        public const float AirPumpIncome = 2f;
        public const float AirPumpReputation = 0.002f;
        public const float WaterMachineIncome = 3f;

        public static PropInfo Get(PropType type) => type switch
        {
            PropType.TrashBin => new PropInfo { Cost = 80f, RequiredLevel = 1, Radius = 6f },
            PropType.Bench => new PropInfo { Cost = 120f, RequiredLevel = 1 },
            PropType.Planter => new PropInfo { Cost = 60f, RequiredLevel = 1 },
            PropType.Lamp => new PropInfo { Cost = 200f, RequiredLevel = 2 },
            PropType.RoadSign => new PropInfo { Cost = 400f, RequiredLevel = 2 },
            PropType.AirPump => new PropInfo { Cost = 350f, RequiredLevel = 3 },
            PropType.WaterMachine => new PropInfo { Cost = 500f, RequiredLevel = 3 },
            _ => new PropInfo { Cost = 600f, RequiredLevel = 4, Radius = 12f }
        };

        public static float Refund(PropType type) => math.round(Get(type).Cost * RefundShare);

        public static float Snap(float value) => math.round(value / GridStep) * GridStep;

        public static float3 Snap(float3 position) => new(Snap(position.x), 0f, Snap(position.z));

        /// <summary>Checks everything but money and level.</summary>
        public static PlacementError CheckSpot(float2 position, BuildArea area, IReadOnlyList<NoBuildZone> zones,
            IReadOnlyList<float2> others)
        {
            if (math.any(position < area.Min) || math.any(position > area.Max))
                return PlacementError.OutsideLot;

            if (zones != null)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    if (math.all(position >= zones[i].Min) && math.all(position <= zones[i].Max))
                        return PlacementError.Blocked;
                }
            }

            if (others == null)
                return PlacementError.None;

            if (others.Count >= MaxProps)
                return PlacementError.TooMany;

            for (int i = 0; i < others.Count; i++)
            {
                if (math.distancesq(position, others[i]) < MinSpacing * MinSpacing)
                    return PlacementError.TooClose;
            }

            return PlacementError.None;
        }

        public static PlacementError Check(PropType type, float2 position, BuildArea area, IReadOnlyList<NoBuildZone> zones,
            IReadOnlyList<float2> others, int stationLevel, float money)
        {
            var spot = CheckSpot(position, area, zones, others);
            if (spot != PlacementError.None)
                return spot;
            var info = Get(type);
            if (stationLevel < info.RequiredLevel)
                return PlacementError.NeedsLevel;
            return money < info.Cost ? PlacementError.NoMoney : PlacementError.None;
        }

        // ---------------------------------------------------------------- effects

        /// <summary>Litter dropped near a trash bin: most of it goes into the bin instead.</summary>
        public static float LitterFactor(bool nearBin) => nearBin ? BinLitterFactor : 1f;

        /// <summary>Chance that a camera near the pump catches a thief who would have escaped.</summary>
        public static float CameraCatchChance(int camerasNear) => math.min(0.75f, 0.35f * camerasNear);

        /// <summary>Lamps scare off night vandals and thieves.</summary>
        public static float NightCrimeFactor(int lamps) => math.max(0.3f, 1f - 0.12f * lamps);

        /// <summary>Benches make waiting easier: up to +20% patience.</summary>
        public static float PatienceFactor(int benches) => 1f + 0.04f * math.min(benches, 5);

        /// <summary>Road signs bring drivers in: up to +18% traffic.</summary>
        public static float SignTrafficFactor(int signs) => 1f + 0.06f * math.min(signs, 3);

        /// <summary>Flower planters make the lot nicer: up to +20% traffic.</summary>
        public static float PlanterTrafficFactor(int planters) => 1f + 0.02f * math.min(planters, 10);

        /// <summary>Per paying customer: chance that someone also uses a tire air pump.</summary>
        public static float AirPumpChance(int pumps) => 0.12f * math.min(pumps, 2);

        /// <summary>Per paying customer: chance of buying a bottle from a water machine.</summary>
        public static float WaterMachineChance(int machines) => 0.1f * math.min(machines, 2);

        public static float TrafficFactor(in PropEffects effects) =>
            SignTrafficFactor(effects.RoadSigns) * PlanterTrafficFactor(effects.Planters);
    }
}
