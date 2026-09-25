using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    /// <summary>Owner skills: costs, order and effects.</summary>
    public static class SkillMath
    {
        public static bool Has(int learned, OwnerSkill skill) => (learned & (1 << (int)skill)) != 0;

        public static int Learn(int learned, OwnerSkill skill) => learned | (1 << (int)skill);

        public static int Count(int learned)
        {
            int count = 0;
            for (int i = 0; i < OwnerSkills.Count; i++)
                count += (learned >> i) & 1;
            return count;
        }

        /// <summary>One point per station level above the first.</summary>
        public static int FreePoints(int stationLevel, int learned) => math.max(0, stationLevel - 1 - Count(learned));

        public static int Branch(OwnerSkill skill) => (int)skill / OwnerSkills.PerBranch;

        /// <summary>The first skill of a branch is always open; the others need the one before.</summary>
        public static bool Unlocked(int learned, OwnerSkill skill) =>
            (int)skill % OwnerSkills.PerBranch == 0 || Has(learned, (OwnerSkill)((int)skill - 1));

        public static bool CanLearn(int learned, OwnerSkill skill, int stationLevel) =>
            !Has(learned, skill) && Unlocked(learned, skill) && FreePoints(stationLevel, learned) > 0;

        public static float RepairStepFactor(int learned) => Has(learned, OwnerSkill.QuickHands) ? 1.5f : 1f;
        public static float RepairCostFactor(int learned) => Has(learned, OwnerSkill.CheapParts) ? 0.7f : 1f;
        public static float WearFactor(int learned) => Has(learned, OwnerSkill.PumpCare) ? 0.7f : 1f;
        public static float TipFactor(int learned) => Has(learned, OwnerSkill.Smile) ? 1.5f : 1f;
        public static float PatienceFactor(int learned) => Has(learned, OwnerSkill.SmallTalk) ? 1.1f : 1f;
        public static float TrafficFactor(int learned) => Has(learned, OwnerSkill.Celebrity) ? 1.05f : 1f;
        public static float FuelPriceFactor(int learned) => Has(learned, OwnerSkill.Wholesale) ? 0.95f : 1f;
        public static float GoodsPriceFactor(int learned) => Has(learned, OwnerSkill.Haggler) ? 0.9f : 1f;
        public static float ContractPriceFactor(int learned) => Has(learned, OwnerSkill.Negotiator) ? 1.05f : 1f;
        public static float SpeedFactor(int learned) => Has(learned, OwnerSkill.Runner) ? 1.2f : 1f;
        public static float ReachFactor(int learned) => Has(learned, OwnerSkill.LongArms) ? 1.3f : 1f;
        public static float StaffDrainFactor(int learned) => Has(learned, OwnerSkill.Motivator) ? 0.8f : 1f;
    }

    /// <summary>What a star rating needs. Stars bring customers; the EV licence needs three.</summary>
    public struct StarRequirement
    {
        public float Rating;
        public float Cleanliness;
        public int Level;
        /// <summary>How many services (wash, tires, motel, diner, parking, chargers) must be open.</summary>
        public int Services;
    }

    public static class StarMath
    {
        public const int MaxStars = 5;
        public const int EvLicenceStars = 3;
        public const int RatingDays = 7;

        public static StarRequirement Requirement(int star) => star switch
        {
            1 => new StarRequirement { Rating = 0f, Cleanliness = 0.5f, Level = 2, Services = 0 },
            2 => new StarRequirement { Rating = 3.5f, Cleanliness = 0.6f, Level = 3, Services = 1 },
            3 => new StarRequirement { Rating = 4.0f, Cleanliness = 0.7f, Level = 4, Services = 2 },
            4 => new StarRequirement { Rating = 4.3f, Cleanliness = 0.75f, Level = 6, Services = 3 },
            _ => new StarRequirement { Rating = 4.6f, Cleanliness = 0.85f, Level = 8, Services = 5 }
        };

        public static bool Meets(StarRequirement need, float rating, float cleanliness, int level, int services) =>
            rating >= need.Rating && cleanliness >= need.Cleanliness && level >= need.Level && services >= need.Services;

        /// <summary>Stars are earned in order: the first unmet requirement stops the count.</summary>
        public static int Evaluate(float rating, float cleanliness, int level, int services)
        {
            int stars = 0;
            while (stars < MaxStars && Meets(Requirement(stars + 1), rating, cleanliness, level, services))
                stars++;
            return stars;
        }

        public static int Services(in StationUpgrades upgrades) =>
            (upgrades.CarWash > 0 ? 1 : 0) + (upgrades.TireService > 0 ? 1 : 0) + (upgrades.Motel > 0 ? 1 : 0) +
            (upgrades.Diner > 0 ? 1 : 0) + (upgrades.TruckParking > 0 ? 1 : 0) + (upgrades.EvCharger > 0 ? 1 : 0);

        /// <summary>+4% customers per star.</summary>
        public static float TrafficFactor(int stars) => 1f + 0.04f * stars;
    }

    /// <summary>Highway events and what they do to traffic, tourists, fuel sales and prices.</summary>
    public static class RoadMath
    {
        public const float DailyChance = 0.12f;
        public const float AccidentChancePerHour = 0.015f;
        public const float AccidentHours = 2f;
        public const float AfterAccidentHours = 1f;

        public static float Hours(RoadEventKind kind) => kind switch
        {
            RoadEventKind.Accident => AccidentHours,
            RoadEventKind.AfterAccident => AfterAccidentHours,
            RoadEventKind.RoadWorks => 120f,
            RoadEventKind.Festival => 48f,
            RoadEventKind.OilCrisis => 120f,
            _ => 0f
        };

        public static float TrafficFactor(RoadEventKind kind) => kind switch
        {
            RoadEventKind.Accident => 0.05f,
            RoadEventKind.AfterAccident => 2.5f,
            RoadEventKind.RoadWorks => 0.7f,
            RoadEventKind.Festival => 1.3f,
            RoadEventKind.OilCrisis => 0.9f,
            _ => 1f
        };

        public static bool TouristBoost(RoadEventKind kind) => kind == RoadEventKind.Festival;

        public static float LitersFactor(RoadEventKind kind) => kind == RoadEventKind.OilCrisis ? 0.7f : 1f;

        /// <summary>The market price drifts towards base × this factor.</summary>
        public static float MarketFactor(RoadEventKind kind) => kind == RoadEventKind.OilCrisis ? 1.25f : 1f;

        /// <summary>A multi-day event for tomorrow: road works, a festival or an oil crisis.</summary>
        public static RoadEventKind PickDaily(float random01) =>
            random01 < 0.4f ? RoadEventKind.RoadWorks : random01 < 0.75f ? RoadEventKind.Festival : RoadEventKind.OilCrisis;
    }

    public struct HostedEventInfo
    {
        public float Cost;
        public int StartHour;
        public int EndHour;
        public float Ticket;
        public float Crowd;
        public int RequiredLevel;
    }

    /// <summary>Events the player hosts: a crowd for a few hours, ticket money, reputation if well prepared.</summary>
    public static class HostedEventMath
    {
        public const int EveryDays = 7;

        public static HostedEventInfo Get(HostedEventKind kind) => kind switch
        {
            HostedEventKind.Fair => new HostedEventInfo { Cost = 400f, StartHour = 10, EndHour = 18, Ticket = 3f, Crowd = 1.8f, RequiredLevel = 3 },
            HostedEventKind.CarMeet => new HostedEventInfo { Cost = 600f, StartHour = 14, EndHour = 20, Ticket = 5f, Crowd = 1.7f, RequiredLevel = 4 },
            HostedEventKind.MovieNight => new HostedEventInfo { Cost = 500f, StartHour = 20, EndHour = 24, Ticket = 8f, Crowd = 1.5f, RequiredLevel = 5 },
            _ => default
        };

        public static bool IsOn(HostedEventKind kind, float hour)
        {
            var info = Get(kind);
            return kind != HostedEventKind.None && hour >= info.StartHour && hour < info.EndHour;
        }

        public static bool CanPlan(int day, int lastHosted) => day - lastHosted >= EveryDays;

        /// <summary>
        /// How well the station was prepared, 0..1: cleanliness counts half, stocked shelves 30%, somebody on
        /// shift 20%.
        /// </summary>
        public static float Preparation(float cleanliness, float shelvesFull, bool staffOnShift) =>
            math.saturate(0.5f * cleanliness + 0.3f * shelvesFull + (staffOnShift ? 0.2f : 0f));

        /// <summary>Reputation after the event: up to +5% for a great one, down to −5% for a mess.</summary>
        public static float ReputationDelta(float preparation) => (preparation - 0.5f) * 0.1f;
    }
}
