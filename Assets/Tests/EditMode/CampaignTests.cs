using System;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class CampaignTests
    {
        [Test]
        public void Chapters_AddUpToTheWholeDebt()
        {
            Assert.AreEqual(CampaignMath.Debt, CampaignMath.Goal(CampaignMath.Chapters).Repaid);
            for (int chapter = 2; chapter <= CampaignMath.Chapters; chapter++)
            {
                Assert.Greater(CampaignMath.Goal(chapter).Deadline, CampaignMath.Goal(chapter - 1).Deadline);
                Assert.Greater(CampaignMath.Goal(chapter).Repaid, CampaignMath.Goal(chapter - 1).Repaid);
            }
        }

        [Test]
        public void Goals_NeedMoneyAndLevel()
        {
            var first = CampaignMath.Goal(1);
            Assert.IsFalse(CampaignMath.Met(first, 10000f, 2));
            Assert.IsFalse(CampaignMath.Met(first, 9000f, 5));
            Assert.IsTrue(CampaignMath.Met(first, 10000f, 3));
            Assert.IsFalse(CampaignMath.Missed(first, 10));
            Assert.IsTrue(CampaignMath.Missed(first, 11));
        }

        [Test]
        public void Payments_NeverExceedTheDebtOrTheMoney()
        {
            Assert.AreEqual(1000f, CampaignMath.Payment(1000f, 0f, 5000f));
            Assert.AreEqual(500f, CampaignMath.Payment(1000f, 0f, 500f));
            Assert.AreEqual(200f, CampaignMath.Payment(1000f, CampaignMath.Debt - 200f, 5000f));
            Assert.AreEqual(0f, CampaignMath.Payment(1000f, 0f, -50f));
            Assert.AreEqual(0f, CampaignMath.Remaining(CampaignMath.Debt + 10f));
        }

        [Test]
        public void OldSave_IsFreePlay()
        {
            var data = JsonUtility.FromJson<SaveData>("{\"version\":21,\"day\":5}");
            Assert.AreEqual((int)GameMode.Free, data.gameMode);
            Assert.IsFalse(data.campaignActive);
            Assert.AreEqual(1, data.campaignChapter);
        }

        [Test]
        public void EveryLetterAndEnding_IsLocalized()
        {
            for (int chapter = 1; chapter <= CampaignMath.Chapters; chapter++)
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"letter.{chapter}.title"));
                Assert.IsTrue(LocTable.Entries.ContainsKey($"letter.{chapter}.text"));
            }

            foreach (CampaignOutcome outcome in Enum.GetValues(typeof(CampaignOutcome)))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey($"letter.end.{outcome}"), outcome.ToString());
                Assert.IsTrue(LocTable.Entries.ContainsKey($"letter.end.{outcome}.text"), outcome.ToString());
            }
        }
    }
}
