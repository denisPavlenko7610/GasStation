using GasStation.Components;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Events that tie a review to who gave it: a regular (by name) or the incognito critic.</summary>
    public static class VisitOutcome
    {
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
