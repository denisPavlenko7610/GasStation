using Unity.Entities;

namespace GasStation.Components
{
    public enum StaffRole : byte
    {
        Attendant = 0,
        Janitor = 1,
        Mechanic = 2,
        Cashier = 3
    }

    public static class StaffRoles
    {
        public const int Count = 4;
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
    }

    /// <summary>Total skill working in each role, recomputed every frame.</summary>
    public struct StaffPower : IComponentData
    {
        public float Attendant;
        public float Janitor;
        public float Mechanic;
        public float Cashier;
        public int Headcount;

        public float Get(StaffRole role) => role switch
        {
            StaffRole.Attendant => Attendant,
            StaffRole.Janitor => Janitor,
            StaffRole.Mechanic => Mechanic,
            StaffRole.Cashier => Cashier,
            _ => 0f
        };
    }

    public struct StaffRoster : IComponentData
    {
        public int NextId;
        public Unity.Mathematics.Random Random;
    }
}
