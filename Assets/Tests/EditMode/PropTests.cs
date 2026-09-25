using System;
using System.Collections.Generic;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GasStation.Tests
{
    public class PropTests
    {
        private static readonly BuildArea Area = new() { Min = new float2(-10f, -10f), Max = new float2(10f, 10f) };
        private static readonly List<NoBuildZone> Zones = new() { new NoBuildZone { Min = new float2(-2f, -2f), Max = new float2(2f, 2f) } };

        [Test]
        public void Spot_OutsideTheLot_IsRejected()
        {
            Assert.AreEqual(PlacementError.OutsideLot, PropMath.CheckSpot(new float2(11f, 0f), Area, Zones, new List<float2>()));
        }

        [Test]
        public void Spot_InANoBuildZone_IsBlocked()
        {
            Assert.AreEqual(PlacementError.Blocked, PropMath.CheckSpot(new float2(1f, 1f), Area, Zones, new List<float2>()));
        }

        [Test]
        public void Spot_NextToAnotherProp_IsTooClose()
        {
            var others = new List<float2> { new(5f, 5f) };
            Assert.AreEqual(PlacementError.TooClose, PropMath.CheckSpot(new float2(5.5f, 5f), Area, Zones, others));
            Assert.AreEqual(PlacementError.None, PropMath.CheckSpot(new float2(7f, 5f), Area, Zones, others));
        }

        [Test]
        public void Lot_HoldsALimitedNumberOfProps()
        {
            var others = new List<float2>();
            for (int i = 0; i < PropMath.MaxProps; i++)
                others.Add(new float2(100f + i * 5f, 0f));
            Assert.AreEqual(PlacementError.TooMany, PropMath.CheckSpot(new float2(5f, 5f), Area, Zones, others));
        }

        [Test]
        public void Check_WantsLevelAndMoney()
        {
            var others = new List<float2>();
            var camera = PropMath.Get(PropType.SecurityCamera);
            Assert.AreEqual(PlacementError.NeedsLevel,
                PropMath.Check(PropType.SecurityCamera, new float2(5f, 5f), Area, Zones, others, camera.RequiredLevel - 1, 1e6f));
            Assert.AreEqual(PlacementError.NoMoney,
                PropMath.Check(PropType.SecurityCamera, new float2(5f, 5f), Area, Zones, others, camera.RequiredLevel, camera.Cost - 1f));
            Assert.AreEqual(PlacementError.None,
                PropMath.Check(PropType.SecurityCamera, new float2(5f, 5f), Area, Zones, others, camera.RequiredLevel, camera.Cost));
        }

        [Test]
        public void Snap_UsesTheGrid()
        {
            var snapped = PropMath.Snap(new float3(1.26f, 3f, -0.74f));
            Assert.AreEqual(1.5f, snapped.x, 0.001f);
            Assert.AreEqual(0f, snapped.y, 0.001f);
            Assert.AreEqual(-0.5f, snapped.z, 0.001f);
        }

        [Test]
        public void Refund_IsHalfThePrice()
        {
            foreach (PropType type in Enum.GetValues(typeof(PropType)))
                Assert.AreEqual(math.round(PropMath.Get(type).Cost / 2f), PropMath.Refund(type));
        }

        [Test]
        public void Effects_HelpButAreCapped()
        {
            Assert.Less(PropMath.LitterFactor(true), PropMath.LitterFactor(false));
            Assert.AreEqual(0f, PropMath.CameraCatchChance(0));
            Assert.LessOrEqual(PropMath.CameraCatchChance(10), 0.75f);
            Assert.GreaterOrEqual(PropMath.NightCrimeFactor(100), 0.3f);
            Assert.AreEqual(PropMath.PatienceFactor(5), PropMath.PatienceFactor(50));
            Assert.AreEqual(PropMath.SignTrafficFactor(3), PropMath.SignTrafficFactor(30));
            Assert.AreEqual(PropMath.PlanterTrafficFactor(10), PropMath.PlanterTrafficFactor(30));
            Assert.AreEqual(1f, PropMath.TrafficFactor(default));
        }

        [Test]
        public void Counts_MatchTypes()
        {
            var effects = new PropEffects();
            foreach (PropType type in Enum.GetValues(typeof(PropType)))
            {
                effects.Add(type);
                Assert.AreEqual(1, effects.Get(type), type.ToString());
            }

            effects.ClearCounts();
            Assert.AreEqual(0, effects.Get(PropType.Lamp));
            Assert.AreEqual(Enum.GetValues(typeof(PropType)).Length, PropTypes.Count);
        }

        [Test]
        public void EveryProp_HasNameAndDescription()
        {
            foreach (PropType type in Enum.GetValues(typeof(PropType)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"prop.name.{type}"), type.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"prop.desc.{type}"), type.ToString());
            }

            foreach (var error in new[] { PlacementError.OutsideLot, PlacementError.Blocked, PlacementError.TooClose, PlacementError.TooMany })
                Assert.IsTrue(LocTable.Entries.ContainsKey($"build.error.{error}"), error.ToString());
        }

        [Test]
        public void PropSave_RoundTrips()
        {
            var prop = new PlacedProp { Id = 7, Type = PropType.Lamp, Position = new float3(3.5f, 0f, -2f), Yaw = 90f };
            var json = JsonUtility.ToJson(PropSaveData.From(prop));
            var restored = JsonUtility.FromJson<PropSaveData>(json).ToProp(1);
            Assert.AreEqual(PropType.Lamp, restored.Type);
            Assert.AreEqual(3.5f, restored.Position.x);
            Assert.AreEqual(-2f, restored.Position.z);
            Assert.AreEqual(90f, restored.Yaw);
            Assert.AreEqual(1, restored.Id);
        }
    }
}
