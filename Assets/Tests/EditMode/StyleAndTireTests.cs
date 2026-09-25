using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class StyleAndTireTests
    {
        [Test]
        public void BetterPaint_AttractsMoreAndCostsMore()
        {
            for (int scheme = 1; scheme < StyleMath.SchemeCount; scheme++)
            {
                Assert.Greater(StyleMath.TrafficFactor(scheme), StyleMath.TrafficFactor(scheme - 1));
                if (scheme > 1)
                    Assert.Greater(StyleMath.Cost(scheme), StyleMath.Cost(scheme - 1));
            }
        }

        [Test]
        public void PeelingPaint_ScaresCustomers()
        {
            Assert.Less(StyleMath.TrafficFactor(0), 1f);
        }

        [Test]
        public void TireService_LevelsAreFasterAndPricier()
        {
            Assert.Less(ShopMath.TireDuration(8f, 3), ShopMath.TireDuration(8f, 1));
            Assert.Greater(ShopMath.TirePrice(25f, 2), ShopMath.TirePrice(25f, 1));
        }

        [Test]
        public void Truckers_NeedTiresMoreOften()
        {
            Assert.Greater(CustomerProfiles.Get(CustomerType.Trucker).TireChance, CustomerProfiles.Get(CustomerType.Regular).TireChance);
            Assert.AreEqual(0f, CustomerProfiles.Get(CustomerType.Thief).TireChance);
        }

        [Test]
        public void PaintQuest_UsesScheme()
        {
            var quest = new QuestDefinition { Goal = QuestGoal.PaintStation, Target = 1f };
            Assert.AreEqual(0f, QuestCatalog.Progress(quest, 0f, 1f, 1f, 0f, 0, 1, 0, 0));
            Assert.IsTrue(QuestCatalog.IsComplete(quest, QuestCatalog.Progress(quest, 0f, 1f, 1f, 0f, 0, 1, 0, 2)));
        }
    }
}
