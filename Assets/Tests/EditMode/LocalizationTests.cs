using System;
using System.Linq;
using System.Text.RegularExpressions;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class LocalizationTests
    {
        [Test]
        public void EveryEntry_HasRussianAndEnglish()
        {
            foreach (var entry in LocTable.Entries)
            {
                Assert.AreEqual(2, entry.Value.Length, entry.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value[(int)GameLanguage.Russian]), entry.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value[(int)GameLanguage.English]), entry.Key);
            }
        }

        [Test]
        public void EnglishTexts_HaveNoCyrillic()
        {
            foreach (var entry in LocTable.Entries)
                Assert.IsFalse(Regex.IsMatch(entry.Value[(int)GameLanguage.English], "[А-Яа-яЁё]"), entry.Key);
        }

        [Test]
        public void BothLanguages_UseTheSamePlaceholders()
        {
            foreach (var entry in LocTable.Entries)
                CollectionAssert.AreEquivalent(Placeholders(entry.Value[0]), Placeholders(entry.Value[1]), entry.Key);
        }

        [Test]
        public void EveryEnumValue_HasText()
        {
            foreach (var key in Enum.GetNames(typeof(FuelType)).Select(n => $"fuel.{n}")
                         .Concat(Enum.GetNames(typeof(ProductType)).Select(n => $"product.{n}"))
                         .Concat(Enum.GetNames(typeof(UpgradeType)).Select(n => $"upgrade.name.{n}"))
                         .Concat(Enum.GetNames(typeof(StaffRole)).Select(n => $"role.{n}"))
                         .Concat(Enum.GetNames(typeof(CustomerType)).Select(n => $"customer.{n}"))
                         .Concat(Enum.GetNames(typeof(QuestGoal)).Select(n => $"quest.goal.{n}")))
            {
                Assert.IsTrue(LocTable.Entries.ContainsKey(key), key);
            }

            foreach (var type in UpgradeMath.Purchasable)
                Assert.IsTrue(LocTable.Entries.ContainsKey($"upgrade.desc.{type}"), type.ToString());
        }

        [Test]
        public void EveryStoryQuest_HasTitle()
        {
            for (int i = 0; i < QuestCatalog.StoryCount; i++)
                Assert.IsTrue(LocTable.Entries.ContainsKey($"quest.title.{QuestCatalog.Get(i).Id}"), $"quest {i}");
        }

        [Test]
        public void EveryStaffName_HasText()
        {
            for (int i = 0; i < StaffMath.NameCount; i++)
                Assert.IsTrue(LocTable.Entries.ContainsKey($"staff.name.{i}"), i.ToString());
        }

        [Test]
        public void Formats_AcceptTheirArguments()
        {
            var args = Enumerable.Repeat((object)1f, 10).ToArray();
            foreach (var entry in LocTable.Entries)
            {
                foreach (var text in entry.Value)
                    Assert.DoesNotThrow(() => string.Format(text, args), entry.Key);
            }
        }

        private static string[] Placeholders(string text) =>
            Regex.Matches(text, @"\{(\d+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
    }
}
