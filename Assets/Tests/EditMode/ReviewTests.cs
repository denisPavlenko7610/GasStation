using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using GasStation.Save;
using NUnit.Framework;
using UnityEngine;

namespace GasStation.Tests
{
    public class ReviewTests
    {
        [Test]
        public void PerfectVisit_GetsFiveStars()
        {
            Assert.AreEqual(5, ReviewMath.Stars(1f, 1.5f, 1.5f, 1f));
        }

        [Test]
        public void WaitingPricesAndDirt_TakeStarsAway()
        {
            int perfect = ReviewMath.Stars(1f, 1.5f, 1.5f, 1f);
            Assert.Less(ReviewMath.Stars(0f, 1.5f, 1.5f, 1f), perfect);
            Assert.Less(ReviewMath.Stars(1f, 2f, 1.5f, 1f), perfect);
            Assert.Less(ReviewMath.Stars(1f, 1.5f, 1.5f, 0f), perfect);
        }

        [Test]
        public void Stars_StayBetweenOneAndFive()
        {
            Assert.AreEqual(1, ReviewMath.Stars(0f, 10f, 1f, 0f));
            Assert.AreEqual(5, ReviewMath.Stars(1f, 0.5f, 1f, 1f));
        }

        [Test]
        public void EveryReviewText_Exists()
        {
            for (int stars = 1; stars <= 5; stars++)
            {
                for (int variant = 0; variant < ReviewMath.VariantsPerStar; variant++)
                    Assert.IsTrue(LocTable.Entries.ContainsKey($"review.{stars}.{variant}"), $"{stars}/{variant}");
            }
        }

        [Test]
        public void ReviewEvents_CountIntoStats()
        {
            var stats = new StationStats();
            AchievementCatalog.Count(ref stats, StationEventType.CustomerReview, 4f);
            AchievementCatalog.Count(ref stats, StationEventType.CustomerReview, 2f);
            Assert.AreEqual(2, stats.RatingCount);
            Assert.AreEqual(6f, stats.RatingSum);
        }

        [Test]
        public void DayHistory_SurvivesSaveRoundTrip()
        {
            var data = new SaveData
            {
                history = new[] { DayHistorySaveData.From(new DayHistoryEntry { Day = 3, Income = 900f, Expenses = 400f, Rating = 4.2f }) }
            };

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var entry = loaded.history[0].ToEntry();
            Assert.AreEqual(3, entry.Day);
            Assert.AreEqual(900f, entry.Income);
            Assert.AreEqual(4.2f, entry.Rating, 1e-4f);
        }
    }
}
