using GasStation.Components;
using GasStation.Logic;
using NUnit.Framework;

namespace GasStation.Tests
{
    public class FacilityTests
    {
        [Test]
        public void OpenSpots_TwoPerLevel_LimitedBySpots()
        {
            Assert.AreEqual(0, FacilityMath.OpenSpots(0, 4));
            Assert.AreEqual(2, FacilityMath.OpenSpots(1, 4));
            Assert.AreEqual(4, FacilityMath.OpenSpots(2, 4));
            Assert.AreEqual(3, FacilityMath.OpenSpots(2, 3));
        }

        [TestCase(20f, ExpectedResult = true)]
        [TestCase(2f, ExpectedResult = true)]
        [TestCase(12f, ExpectedResult = false)]
        public bool Truckers_SleepAtNight(float hour) => FacilityMath.WantsToSleep(hour);

        [Test]
        public void ParkedTrucker_LeavesInTheMorningOnly()
        {
            Assert.IsFalse(FacilityMath.ShouldLeaveParking(21f, 6f), "evening of arrival");
            Assert.IsFalse(FacilityMath.ShouldLeaveParking(3f, 6f), "night");
            Assert.IsTrue(FacilityMath.ShouldLeaveParking(6.5f, 6f), "morning");
        }

        [Test]
        public void DirtyRestroom_LowersCleanliness()
        {
            Assert.AreEqual(1f, FacilityMath.CombinedCleanliness(1f, 0f));
            Assert.AreEqual(0.6f, FacilityMath.CombinedCleanliness(1f, 1f), 1e-5f);
        }

        [Test]
        public void SupplyManager_ReordersBelowQuarter()
        {
            Assert.IsTrue(FacilityMath.NeedsReorder(400f, 2000f));
            Assert.IsFalse(FacilityMath.NeedsReorder(600f, 2000f));
            Assert.IsFalse(FacilityMath.NeedsReorder(0f, 0f));
        }

        [Test]
        public void SupplyManager_HigherLevelsAreCheaperAndFaster()
        {
            Assert.Less(FacilityMath.SupplyDiscount(2), FacilityMath.SupplyDiscount(1));
            Assert.Less(FacilityMath.SupplyDeliveryFactor(3), FacilityMath.SupplyDeliveryFactor(2));
        }

        [Test]
        public void Motel_OpensTwoRoomsPerLevel_AndCostsMoreWithLevel()
        {
            Assert.AreEqual(0, FacilityMath.OpenRooms(0, 6));
            Assert.AreEqual(4, FacilityMath.OpenRooms(2, 6));
            Assert.AreEqual(6, FacilityMath.OpenRooms(3, 6));
            Assert.Greater(FacilityMath.RoomPrice(40f, 3), FacilityMath.RoomPrice(40f, 1));
        }

        [TestCase(20f, ExpectedResult = true)]
        [TestCase(1f, ExpectedResult = true)]
        [TestCase(10f, ExpectedResult = false)]
        public bool Travellers_WantRoomsInTheEvening(float hour) => FacilityMath.WantsRoom(hour);

        [Test]
        public void OnlyRegularsAndTourists_StayAtTheMotel()
        {
            Assert.Greater(CustomerProfiles.Get(CustomerType.Tourist).MotelChance, 0f);
            Assert.Greater(CustomerProfiles.Get(CustomerType.Regular).MotelChance, 0f);
            Assert.AreEqual(0f, CustomerProfiles.Get(CustomerType.Trucker).MotelChance, "truckers use the truck parking");
            Assert.AreEqual(0f, CustomerProfiles.Get(CustomerType.Thief).MotelChance);
        }

        [Test]
        public void TruckParking_HasTwoLevels()
        {
            Assert.AreEqual(2, UpgradeMath.MaxLevel(UpgradeType.TruckParking));
        }
    }
}
