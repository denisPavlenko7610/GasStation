using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public enum AchievementId : byte
    {
        FirstCustomer,
        Served100,
        Served1000,
        Trash100,
        Trash1000,
        Income10K,
        Income100K,
        Level5,
        Level10,
        ThiefCaught,
        MotelGuests10,
        Truckers10,
        CarsWashed50,
        Tires25,
        PerfectDay,
        Tycoon,
        Spotless,
        FullStaff,
        FullyRenovated
    }

    /// <summary>Everything an achievement can look at.</summary>
    public struct AchievementContext
    {
        public StationStats Stats;
        public int Level;
        public float Money;
        public float Cleanliness;
        public int Headcount;
        public int RenovationsDone;
        public int RenovationsTotal;
    }

    public static class AchievementCatalog
    {
        public const int Count = 19;

        /// <summary>Current value and target of an achievement.</summary>
        public static float2 Progress(AchievementId id, in AchievementContext c) => id switch
        {
            AchievementId.FirstCustomer => new float2(c.Stats.Served, 1),
            AchievementId.Served100 => new float2(c.Stats.Served, 100),
            AchievementId.Served1000 => new float2(c.Stats.Served, 1000),
            AchievementId.Trash100 => new float2(c.Stats.TrashCollected, 100),
            AchievementId.Trash1000 => new float2(c.Stats.TrashCollected, 1000),
            AchievementId.Income10K => new float2(c.Stats.Income, 10000),
            AchievementId.Income100K => new float2(c.Stats.Income, 100000),
            AchievementId.Level5 => new float2(c.Level, 5),
            AchievementId.Level10 => new float2(c.Level, 10),
            AchievementId.ThiefCaught => new float2(c.Stats.ThievesCaught, 1),
            AchievementId.MotelGuests10 => new float2(c.Stats.MotelGuests, 10),
            AchievementId.Truckers10 => new float2(c.Stats.TruckersHosted, 10),
            AchievementId.CarsWashed50 => new float2(c.Stats.CarsWashed, 50),
            AchievementId.Tires25 => new float2(c.Stats.TiresChanged, 25),
            AchievementId.PerfectDay => new float2(c.Stats.PerfectDays, 1),
            AchievementId.Tycoon => new float2(c.Money, 50000),
            AchievementId.Spotless => new float2(math.floor(c.Cleanliness * 100f), 100),
            AchievementId.FullStaff => new float2(c.Headcount, StaffMath.MaxStaff),
            // Without renovations in the scene there is nothing to finish.
            AchievementId.FullyRenovated => new float2(c.RenovationsDone, c.RenovationsTotal > 0 ? c.RenovationsTotal : int.MaxValue),
            _ => new float2(0, 1)
        };

        public static bool IsReached(AchievementId id, in AchievementContext context)
        {
            var progress = Progress(id, context);
            return progress.x >= progress.y;
        }

        /// <summary>A perfect day: at least 20 customers served and nobody left unhappy.</summary>
        public static bool IsPerfectDay(DayReport report) => report.Served >= 20 && report.Lost == 0;

        /// <summary>Money earned from customers (not quest rewards).</summary>
        public static bool IsIncome(StationEventType type) => type is StationEventType.CustomerPaid
            or StationEventType.TipReceived or StationEventType.ShopSale or StationEventType.CarWashed
            or StationEventType.ParkingPaid or StationEventType.MotelPaid or StationEventType.TiresChanged
            or StationEventType.InspectionPassed;

        public static void Count(ref StationStats stats, StationEventType type, float value)
        {
            if (IsIncome(type))
                stats.Income += value;

            switch (type)
            {
                case StationEventType.CustomerPaid: stats.Served++; break;
                case StationEventType.TrashCollected: stats.TrashCollected++; break;
                case StationEventType.ThiefCaught: stats.ThievesCaught++; break;
                case StationEventType.MotelPaid: stats.MotelGuests++; break;
                case StationEventType.ParkingPaid: stats.TruckersHosted++; break;
                case StationEventType.TiresChanged: stats.TiresChanged++; break;
                case StationEventType.CarWashed: stats.CarsWashed++; break;
            }
        }
    }
}
