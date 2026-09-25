using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class GrowthTests
    {
        [Test]
        public void SkillPoints_ComeWithStationLevels()
        {
            Assert.AreEqual(0, SkillMath.FreePoints(1, 0));
            Assert.AreEqual(2, SkillMath.FreePoints(3, 0));
            int learned = SkillMath.Learn(0, OwnerSkill.QuickHands);
            Assert.AreEqual(1, SkillMath.FreePoints(3, learned));
            Assert.AreEqual(1, SkillMath.Count(learned));
        }

        [Test]
        public void Skills_NeedThePreviousOneInTheBranch()
        {
            Assert.IsTrue(SkillMath.CanLearn(0, OwnerSkill.QuickHands, 5));
            Assert.IsFalse(SkillMath.CanLearn(0, OwnerSkill.CheapParts, 5));
            int learned = SkillMath.Learn(0, OwnerSkill.QuickHands);
            Assert.IsTrue(SkillMath.CanLearn(learned, OwnerSkill.CheapParts, 5));
            Assert.IsFalse(SkillMath.CanLearn(learned, OwnerSkill.QuickHands, 5), "already learned");
            Assert.IsFalse(SkillMath.CanLearn(0, OwnerSkill.Smile, 1), "no points at level 1");
            Assert.AreEqual(1, SkillMath.Branch(OwnerSkill.SmallTalk));
        }

        [Test]
        public void Skills_ChangeTheirNumbers()
        {
            int all = 0;
            for (int i = 0; i < OwnerSkills.Count; i++)
                all = SkillMath.Learn(all, (OwnerSkill)i);
            Assert.Greater(SkillMath.RepairStepFactor(all), 1f);
            Assert.Less(SkillMath.RepairCostFactor(all), 1f);
            Assert.Less(SkillMath.WearFactor(all), 1f);
            Assert.Greater(SkillMath.TipFactor(all), 1f);
            Assert.Less(SkillMath.FuelPriceFactor(all), 1f);
            Assert.Greater(SkillMath.ContractPriceFactor(all), 1f);
            Assert.Greater(SkillMath.SpeedFactor(all), 1f);
            Assert.Less(SkillMath.StaffDrainFactor(all), 1f);
            Assert.AreEqual(1f, SkillMath.TipFactor(0));
            Assert.AreEqual(Enum.GetValues(typeof(OwnerSkill)).Length, OwnerSkills.Count);
        }

        [Test]
        public void Stars_AreEarnedInOrder()
        {
            Assert.AreEqual(0, StarMath.Evaluate(5f, 1f, 1, 6));
            Assert.AreEqual(1, StarMath.Evaluate(0f, 0.6f, 2, 0));
            Assert.AreEqual(2, StarMath.Evaluate(3.6f, 0.65f, 3, 1));
            Assert.AreEqual(2, StarMath.Evaluate(4.5f, 0.9f, 3, 6), "level 3 stops at two stars");
            Assert.AreEqual(5, StarMath.Evaluate(4.7f, 0.9f, 10, 6));
            Assert.Greater(StarMath.TrafficFactor(3), StarMath.TrafficFactor(1));
        }

        [Test]
        public void RoadEvents_ChangeTheRoad()
        {
            Assert.Less(RoadMath.TrafficFactor(RoadEventKind.Accident), 0.1f);
            Assert.Greater(RoadMath.TrafficFactor(RoadEventKind.AfterAccident), 2f);
            Assert.Less(RoadMath.TrafficFactor(RoadEventKind.RoadWorks), 1f);
            Assert.IsTrue(RoadMath.TouristBoost(RoadEventKind.Festival));
            Assert.Less(RoadMath.LitersFactor(RoadEventKind.OilCrisis), 1f);
            Assert.Greater(RoadMath.MarketFactor(RoadEventKind.OilCrisis), 1f);
            Assert.AreEqual(RoadEventKind.RoadWorks, RoadMath.PickDaily(0.1f));
            Assert.AreEqual(RoadEventKind.OilCrisis, RoadMath.PickDaily(0.9f));
        }

        [Test]
        public void HostedEvents_RunInTheirHours_OnceAWeek()
        {
            Assert.IsTrue(HostedEventMath.IsOn(HostedEventKind.Fair, 12f));
            Assert.IsFalse(HostedEventMath.IsOn(HostedEventKind.Fair, 19f));
            Assert.IsTrue(HostedEventMath.IsOn(HostedEventKind.MovieNight, 23.5f));
            Assert.IsFalse(HostedEventMath.IsOn(HostedEventKind.MovieNight, 0.5f));
            Assert.IsFalse(HostedEventMath.IsOn(HostedEventKind.None, 12f));
            Assert.IsTrue(HostedEventMath.CanPlan(10, 3));
            Assert.IsFalse(HostedEventMath.CanPlan(9, 3));
        }

        [Test]
        public void Preparation_DecidesTheReputation()
        {
            Assert.AreEqual(1f, HostedEventMath.Preparation(1f, 1f, true), 0.001f);
            Assert.Greater(HostedEventMath.ReputationDelta(1f), 0f);
            Assert.Less(HostedEventMath.ReputationDelta(0f), 0f);
        }

        [Test]
        public void EverythingIsLocalized()
        {
            foreach (OwnerSkill skill in Enum.GetValues(typeof(OwnerSkill)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"skill.{skill}"), skill.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"skill.{skill}.desc"), skill.ToString());
            }

            foreach (RoadEventKind kind in Enum.GetValues(typeof(RoadEventKind)))
            {
                if (kind == RoadEventKind.None)
                    continue;
                foreach (var suffix in new[] { "", ".start", ".end", ".desc" })
                    Assert.IsTrue(LocTable.Entries.ContainsKey($"road.{kind}{suffix}"), $"road.{kind}{suffix}");
            }

            foreach (var kind in new[] { HostedEventKind.Fair, HostedEventKind.CarMeet, HostedEventKind.MovieNight })
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"hosted.{kind}"));
                Assert.IsTrue(LocTable.Entries.ContainsKey($"hosted.{kind}.desc"));
            }
        }
    }
}
