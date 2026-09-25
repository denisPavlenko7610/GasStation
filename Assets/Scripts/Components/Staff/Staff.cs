using Unity.Entities;

namespace GasStation.Components
{
    public enum StaffRole : byte
    {
        Attendant = 0,
        Janitor = 1,
        Mechanic = 2,
        Cashier = 3,
        /// <summary>Cooks at the diner grill.</summary>
        Cook = 4
    }

    public static class StaffRoles
    {
        public const int Count = 5;
    }

    public enum WorkShift : byte
    {
        /// <summary>7:00–19:00.</summary>
        Day = 0,
        /// <summary>19:00–7:00.</summary>
        Night = 1
    }

    public enum StaffTrait : byte
    {
        None = 0,
        /// <summary>Customers like the chat (a little reputation), but works slower.</summary>
        Chatty = 1,
        /// <summary>Slow but thorough: walks slower, does more per job.</summary>
        Pedant = 2,
        /// <summary>Better at night, a bit worse by day.</summary>
        NightOwl = 3,
        /// <summary>Tires slower.</summary>
        Tireless = 4
    }

    public static class StaffTraits
    {
        public const int Count = 5;
    }

    /// <summary>A hired employee. Lives on its own entity.</summary>
    public struct Worker : IComponentData
    {
        public int Id;
        public StaffRole Role;
        /// <summary>0.6..1.5; how much work one person does. Grows with days worked.</summary>
        public float Skill;
        public float Wage;
        /// <summary>0..1, hidden; below a daily roll the worker steals from the till.</summary>
        public float Honesty;
        public int DaysWorked;
        public int NameIndex;
        public WorkShift Shift;
        public StaffTrait Trait;
        /// <summary>0..1; drains on shift, recovers off shift. Tired people work slower.</summary>
        public float Energy;
        /// <summary>0..1; pay, praise and tiredness. Unhappy people work worse and eventually quit.</summary>
        public float Mood;
        /// <summary>Paid courses taken (each adds skill).</summary>
        public int Training;
        /// <summary>Days in a row with a very low mood.</summary>
        public int UnhappyDays;
        /// <summary>Day of the last praise; praise works once a day.</summary>
        public int PraisedDay;
        /// <summary>Energy dropped to empty today (overworked).</summary>
        public bool Exhausted;
    }

    public enum AgentTask : byte
    {
        Idle,
        /// <summary>Walking to the task target.</summary>
        Walking,
        /// <summary>At the target, doing the job (Timer counts down).</summary>
        Working,
        /// <summary>Off shift: walked home and is not on the lot.</summary>
        OffDuty
    }

    public enum AgentJob : byte
    {
        None,
        FuelCar,
        PickTrash,
        CleanRestroom,
        RepairPump,
        /// <summary>Cashier behind the shop counter.</summary>
        Counter,
        /// <summary>Cook at the diner grill.</summary>
        Grill
    }

    /// <summary>
    /// The visible body of a worker: walks to the nearest job of its role (a waiting car, litter, a worn pump,
    /// the dirty restroom) and does it. Moved by CarMoveJob through PathPoint like pedestrians.
    /// </summary>
    public struct StaffAgent : IComponentData
    {
        public int WorkerId;
        public StaffRole Role;
        public AgentTask Task;
        public AgentJob Job;
        /// <summary>Car, trash or pump being worked on; Entity.Null for the restroom or the shop counter.</summary>
        public Entity Target;
        public float Timer;
        /// <summary>Current efficiency of the worker (skill × energy × mood × trait), copied every frame.</summary>
        public float Efficiency;
    }

    /// <summary>Where staff come in and go home: the shop door (or the station origin).</summary>
    public struct StaffSettings : IComponentData
    {
        public Unity.Mathematics.float3 Home;
    }

    /// <summary>Today's applicants. Regenerated every morning.</summary>
    [InternalBufferCapacity(3)]
    public struct StaffCandidate : IBufferElementData
    {
        public StaffRole Role;
        public float Skill;
        public float Wage;
        public float Honesty;
        public int NameIndex;
        public StaffTrait Trait;
    }

    /// <summary>Total skill working in each role, recomputed every frame.</summary>
    public struct StaffPower : IComponentData
    {
        public float Attendant;
        public float Janitor;
        public float Mechanic;
        public float Cashier;
        public float Cook;
        public int Headcount;

        public float Get(StaffRole role) => role switch
        {
            StaffRole.Attendant => Attendant,
            StaffRole.Janitor => Janitor,
            StaffRole.Mechanic => Mechanic,
            StaffRole.Cashier => Cashier,
            StaffRole.Cook => Cook,
            _ => 0f
        };
    }

    public struct StaffRoster : IComponentData
    {
        public int NextId;
        public Unity.Mathematics.Random Random;
    }
}
