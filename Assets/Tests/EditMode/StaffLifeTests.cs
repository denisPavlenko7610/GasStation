using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class StaffLifeTests
    {
        private static Worker Fresh(WorkShift shift = WorkShift.Day, StaffTrait trait = StaffTrait.None) => new()
        {
            Role = StaffRole.Janitor, Skill = 1f, Wage = 60f, Shift = shift, Trait = trait, Energy = 1f, Mood = 0.5f
        };

        [Test]
        public void Shifts_CoverTheWholeDay()
        {
            for (float hour = 0f; hour < 24f; hour += 0.5f)
                Assert.AreNotEqual(StaffMath.OnShift(WorkShift.Day, hour), StaffMath.OnShift(WorkShift.Night, hour), hour.ToString());
            Assert.IsTrue(StaffMath.OnShift(WorkShift.Day, 7f));
            Assert.IsFalse(StaffMath.OnShift(WorkShift.Day, 19f));
            Assert.IsTrue(StaffMath.OnShift(WorkShift.Night, 23f));
        }

        [Test]
        public void NobodyWorks_OffShift()
        {
            Assert.AreEqual(0f, StaffMath.Efficiency(Fresh(WorkShift.Day), 2f));
            Assert.Greater(StaffMath.Efficiency(Fresh(WorkShift.Night), 2f), 0f);
        }

        [Test]
        public void TiredAndUnhappy_WorkWorse()
        {
            var fresh = Fresh();
            var tired = Fresh();
            tired.Energy = 0f;
            var unhappy = Fresh();
            unhappy.Mood = 0f;
            Assert.Less(StaffMath.Efficiency(tired, 12f), StaffMath.Efficiency(fresh, 12f));
            Assert.Less(StaffMath.Efficiency(unhappy, 12f), StaffMath.Efficiency(fresh, 12f));
        }

        [Test]
        public void Traits_DoWhatTheySay()
        {
            Assert.Greater(StaffMath.Efficiency(Fresh(WorkShift.Night, StaffTrait.NightOwl), 23f),
                StaffMath.Efficiency(Fresh(WorkShift.Night), 23f));
            Assert.Less(StaffMath.Efficiency(Fresh(WorkShift.Day, StaffTrait.Chatty), 12f), StaffMath.Efficiency(Fresh(), 12f));
            Assert.Less(StaffMath.WalkFactor(StaffTrait.Pedant), 1f);
            Assert.Greater(StaffMath.JobFactor(StaffTrait.Pedant), 1f);
            Assert.Less(StaffMath.EnergyDrain(StaffTrait.Tireless), StaffMath.EnergyDrain(StaffTrait.None));
        }

        [Test]
        public void Mood_FollowsPayAndTiredness()
        {
            float fair = StaffMath.NextMood(0.5f, 60f, 60f, false);
            float overpaid = StaffMath.NextMood(0.5f, 80f, 60f, false);
            float underpaid = StaffMath.NextMood(0.5f, 40f, 60f, false);
            float exhausted = StaffMath.NextMood(0.5f, 60f, 60f, true);
            Assert.Greater(overpaid, fair);
            Assert.Less(underpaid, fair);
            Assert.Less(exhausted, fair);
            Assert.GreaterOrEqual(underpaid, 0f);
        }

        [Test]
        public void Training_GetsPricier_AndStops()
        {
            Assert.Greater(StaffMath.TrainingCost(1), StaffMath.TrainingCost(0));
            var worker = Fresh();
            worker.Training = StaffMath.MaxTraining;
            Assert.IsFalse(StaffMath.CanTrain(worker));
            Assert.AreEqual(66f, StaffMath.Raise(60f));
        }

        [Test]
        public void WorkerSave_KeepsTheirLife_AndOldSavesStartFresh()
        {
            var worker = Fresh(WorkShift.Night, StaffTrait.Pedant);
            worker.Energy = 0.4f;
            worker.Mood = 0.8f;
            worker.Training = 1;
            var restored = JsonUtility.FromJson<WorkerSaveData>(JsonUtility.ToJson(WorkerSaveData.From(worker))).ToWorker();
            Assert.AreEqual(WorkShift.Night, restored.Shift);
            Assert.AreEqual(StaffTrait.Pedant, restored.Trait);
            Assert.AreEqual(0.4f, restored.Energy, 0.0001f);
            Assert.AreEqual(1, restored.Training);

            var old = JsonUtility.FromJson<WorkerSaveData>("{\"id\":1,\"role\":1,\"skill\":1.0,\"wage\":60}").ToWorker();
            Assert.AreEqual(1f, old.Energy);
            Assert.AreEqual(StaffMath.StartMood, old.Mood);
        }

        [Test]
        public void EveryTraitAndShift_IsLocalized()
        {
            foreach (StaffTrait trait in Enum.GetValues(typeof(StaffTrait)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"trait.{trait}"), trait.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"trait.{trait}.desc"), trait.ToString());
            }

            foreach (WorkShift shift in Enum.GetValues(typeof(WorkShift)))
                Assert.IsTrue(LocTable.Entries.ContainsKey($"shift.{shift}"), shift.ToString());
            Assert.AreEqual(Enum.GetValues(typeof(StaffTrait)).Length, StaffTraits.Count);
        }
    }
}
