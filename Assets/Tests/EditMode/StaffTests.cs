using GasStation.Components;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class StaffTests
    {
        [Test]
        public void Generate_ProducesValidCandidates()
        {
            var random = new Unity.Mathematics.Random(42);
            for (int i = 0; i < 200; i++)
            {
                var candidate = StaffMath.Generate(ref random);
                Assert.That(candidate.Skill >= 0.6f && candidate.Skill <= 1.4f);
                Assert.That(candidate.Honesty >= 0.75f && candidate.Honesty <= 1f);
                Assert.Greater(candidate.Wage, 0f);
                Assert.That(candidate.NameIndex >= 0 && candidate.NameIndex < StaffMath.NameCount);
            }
        }

        [Test]
        public void SkilledWorkers_AskForMore()
        {
            Assert.Greater(StaffMath.Wage(StaffRole.Mechanic, 1.4f), StaffMath.Wage(StaffRole.Mechanic, 0.6f));
        }

        [Test]
        public void Skill_GrowsUpToCap()
        {
            float skill = 1.49f;
            skill = StaffMath.GrowSkill(skill);
            skill = StaffMath.GrowSkill(skill);
            Assert.AreEqual(StaffMath.MaxSkill, skill, 1e-5f);
        }

        [Test]
        public void MoreStaffPower_WorksFaster()
        {
            Assert.IsTrue(float.IsPositiveInfinity(StaffMath.AttendantDelay(0f)));
            Assert.Less(StaffMath.AttendantDelay(2f), StaffMath.AttendantDelay(1f));
            Assert.Less(StaffMath.ShopTimeFactor(1f), StaffMath.ShopTimeFactor(0f));
            Assert.Greater(StaffMath.MechanicRepairPerSecond(1.5f), StaffMath.MechanicRepairPerSecond(1f));
        }

        [Test]
        public void HonestWorkers_NeverSteal()
        {
            Assert.IsFalse(StaffMath.Steals(1f, 0.999f));
            Assert.IsTrue(StaffMath.Steals(0.8f, 0.9f));
        }

        [Test]
        public void OldSave_ConvertsStaffUpgradesToWorkers()
        {
            var data = JsonUtility.FromJson<SaveData>("{\"version\":6}");
            var upgrades = new StationUpgrades { Attendant = 1, Janitor = 2 };
            var economy = new Economy { DailyFixedCosts = 150f + 80f + 2 * 60f };

            var staff = data.RestoreStaff(ref upgrades, ref economy);

            Assert.AreEqual(3, staff.Count);
            Assert.AreEqual(0, upgrades.Attendant);
            Assert.AreEqual(0, upgrades.Janitor);
            Assert.AreEqual(150f, economy.DailyFixedCosts, 1e-3f, "Salaries move from fixed costs to wages");
        }

        [Test]
        public void NewSave_RestoresWorkersAsSaved()
        {
            var data = new SaveData
            {
                workers = new[] { WorkerSaveData.From(new Worker { Id = 7, Role = StaffRole.Cashier, Skill = 1.2f, Wage = 64f, Honesty = 0.9f }) }
            };
            var upgrades = new StationUpgrades();
            var economy = new Economy();

            var staff = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)).RestoreStaff(ref upgrades, ref economy);

            Assert.AreEqual(1, staff.Count);
            Assert.AreEqual(7, staff[0].Id);
            Assert.AreEqual(StaffRole.Cashier, staff[0].Role);
        }
    }
}
