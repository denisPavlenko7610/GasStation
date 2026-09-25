using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>Owner skills. Four branches of three; a skill needs the previous one in its branch.</summary>
    public enum OwnerSkill : byte
    {
        QuickHands = 0,
        CheapParts = 1,
        PumpCare = 2,
        Smile = 3,
        SmallTalk = 4,
        Celebrity = 5,
        Wholesale = 6,
        Haggler = 7,
        Negotiator = 8,
        Runner = 9,
        LongArms = 10,
        Motivator = 11
    }

    public static class OwnerSkills
    {
        public const int Count = 12;
        public const int PerBranch = 3;
    }

    /// <summary>What the owner has learned. One skill point per station level above the first.</summary>
    public struct OwnerSkillSet : IComponentData
    {
        /// <summary>Bit i = OwnerSkill i learned.</summary>
        public int Learned;
        /// <summary>Interaction and pickup radius without skills, captured on the first update.</summary>
        public float BaseInteractionRadius;
        public float BasePickupRadius;
    }

    /// <summary>Station rating in stars (0–5), re-evaluated every night.</summary>
    public struct StationStars : IComponentData
    {
        public int Stars;
        public int Best;
    }

    public enum RoadEventKind : byte
    {
        None,
        /// <summary>An accident closed the highway for a couple of hours.</summary>
        Accident,
        /// <summary>The road just reopened: the queue of cars arrives at once.</summary>
        AfterAccident,
        /// <summary>Road works: a detour takes traffic away for days.</summary>
        RoadWorks,
        /// <summary>A festival in town: tourists everywhere.</summary>
        Festival,
        /// <summary>Fuel gets expensive, drivers buy less.</summary>
        OilCrisis
    }

    /// <summary>What is happening on the highway. Lives on the station entity.</summary>
    public struct RoadEvent : IComponentData
    {
        public RoadEventKind Kind;
        /// <summary>Game hours left.</summary>
        public float HoursLeft;
        public int LastRolledHour;
        public Unity.Mathematics.Random Random;
    }

    public enum HostedEventKind : byte
    {
        None,
        Fair,
        CarMeet,
        MovieNight
    }

    public static class HostedEventKinds
    {
        public const int Count = 4;
    }

    /// <summary>An event the player organizes at the station, at most once a week.</summary>
    public struct HostedEvents : IComponentData
    {
        public HostedEventKind Planned;
        public int PlannedDay;
        public HostedEventKind Active;
        public int Attendees;
        public int LastHostedDay;
    }
}
