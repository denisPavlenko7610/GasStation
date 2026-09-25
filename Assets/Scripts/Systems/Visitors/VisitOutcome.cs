using GasStation.Components;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Events that tie a review to who gave it: a regular (by name) or the incognito critic.</summary>
    public static class VisitOutcome
    {
        /// <summary>A contract vehicle was fueled (served) or left without fuel.</summary>
        public static void ContractResult(DynamicBuffer<StationEvent> events, in Car car, bool served)
        {
            if (car.ContractId > 0)
                StationEvent.Push(events, served ? StationEventType.ContractVehicleServed : StationEventType.ContractVehicleMissed,
                    car.FuelType, 0f, car.ContractId);
        }

        /// <summary>Call right after pushing the CustomerReview for this car.</summary>
        public static void Rate(DynamicBuffer<StationEvent> events, in Car car, float stars)
        {
            if (car.RegularId > 0)
                StationEvent.Push(events, StationEventType.RegularVisit, car.FuelType, stars, car.RegularId);
            if (car.Customer == CustomerType.Critic)
                StationEvent.Push(events, StationEventType.CriticVisit, default, stars);
        }
    }
}
