using GasStation.Components;
using Unity.Mathematics;

namespace GasStation.Logic
{
    public static class StaffMath
    {
        public const int MaxStaff = 6;
        public const int CandidatesPerDay = 3;
        public const float SkillGrowthPerDay = 0.02f;
        public const float MaxSkill = 1.5f;
        public const int NameCount = 16;

        public static float BaseWage(StaffRole role) => role switch
        {
            StaffRole.Attendant => 80f,
            StaffRole.Janitor => 60f,
            StaffRole.Mechanic => 70f,
            StaffRole.Cashier => 60f,
            _ => 60f
        };

        /// <summary>Skilled people ask for more: from 60% of the base wage at skill 0.6 to 120% at 1.4.</summary>
        public static float Wage(StaffRole role, float skill) =>
            math.round(BaseWage(role) * (0.6f + (skill - 0.6f) * 0.75f));

        /// <summary>Hiring costs one day of wages.</summary>
        public static float HiringFee(float wage) => wage;

        public static StaffCandidate Generate(ref Random random)
        {
            var role = (StaffRole)random.NextInt(StaffRoles.Count);
            float skill = math.round(random.NextFloat(0.6f, 1.4f) * 100f) / 100f;
            return new StaffCandidate
            {
                Role = role,
                Skill = skill,
                Wage = Wage(role, skill),
                Honesty = random.NextFloat(0.75f, 1f),
                NameIndex = random.NextInt(NameCount)
            };
        }

        public static float GrowSkill(float skill) => math.min(MaxSkill, skill + SkillGrowthPerDay);

        /// <summary>A worker steals when the daily roll is above his honesty.</summary>
        public static bool Steals(float honesty, float random01) => random01 > honesty;

        /// <summary>References hint at honesty without revealing it.</summary>
        public static int ReferenceGrade(float honesty) => honesty >= 0.93f ? 2 : honesty >= 0.85f ? 1 : 0;

        /// <summary>Seconds before attendants start fueling a waiting car.</summary>
        public static float AttendantDelay(float power) => power > 0f ? 8f / power : float.PositiveInfinity;

        /// <summary>Seconds between two pieces of litter removed by janitors.</summary>
        public static float JanitorInterval(float power) => power > 0f ? 20f / power : float.PositiveInfinity;

        /// <summary>Pump condition restored per second by mechanics.</summary>
        public static float MechanicRepairPerSecond(float power) => 0.01f * power;

        /// <summary>Cashiers make shopping faster: one average cashier halves the time at the till.</summary>
        public static float ShopTimeFactor(float cashierPower) => 1f / (1f + 0.5f * cashierPower);
    }
}
