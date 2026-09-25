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
                NameIndex = random.NextInt(NameCount),
                // Two in five applicants have a noticeable trait.
                Trait = random.NextFloat() < 0.6f ? StaffTrait.None : (StaffTrait)random.NextInt(1, StaffTraits.Count)
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

        // ---------------------------------------------------------------- living staff

        public const int DayShiftStart = 7;
        public const int DayShiftEnd = 19;
        public const float StartMood = 0.6f;
        public const float EnergyDrainPerHour = 0.06f;
        public const float EnergyRecoverPerHour = 0.12f;
        public const float PraiseMood = 0.12f;
        public const float UnhappyMood = 0.2f;
        public const int UnhappyDaysToQuit = 2;
        public const int MaxTraining = 2;
        public const float TrainingSkill = 0.15f;
        public const float RaiseShare = 0.1f;
        public const float ChattyReputation = 0.003f;

        public const float AgentWalkSpeed = 2.4f;
        public const float AttendantServiceTime = 1.5f;
        public const float TrashPickupTime = 1f;
        public const float RestroomDirtyForJanitor = 0.4f;
        public const float RestroomCleaningPerSecond = 0.08f;
        public const float PumpWornForMechanic = 0.8f;
        public const float MechanicRepairRate = 0.03f;

        public static bool OnShift(WorkShift shift, float hour)
        {
            bool day = hour >= DayShiftStart && hour < DayShiftEnd;
            return shift == WorkShift.Day ? day : !day;
        }

        public static bool IsNight(float hour) => hour < DayShiftStart || hour >= DayShiftEnd;

        /// <summary>0.5 when exhausted, 1 when fresh.</summary>
        public static float EnergyFactor(float energy) => 0.5f + 0.5f * math.saturate(energy);

        /// <summary>0.7 when miserable, 1.3 when happy.</summary>
        public static float MoodFactor(float mood) => 0.7f + 0.6f * math.saturate(mood);

        /// <summary>How the trait changes the amount of work done (walking speed is separate).</summary>
        public static float TraitFactor(StaffTrait trait, bool night) => trait switch
        {
            StaffTrait.Chatty => 0.85f,
            StaffTrait.NightOwl => night ? 1.3f : 0.9f,
            _ => 1f
        };

        /// <summary>Pedants walk slower but do more at the spot.</summary>
        public static float WalkFactor(StaffTrait trait) => trait == StaffTrait.Pedant ? 0.8f : 1f;

        public static float JobFactor(StaffTrait trait) => trait == StaffTrait.Pedant ? 1.4f : 1f;

        /// <summary>Work done per second by this worker right now; 0 off shift.</summary>
        public static float Efficiency(in Worker worker, float hour) =>
            !OnShift(worker.Shift, hour)
                ? 0f
                : worker.Skill * EnergyFactor(worker.Energy) * MoodFactor(worker.Mood) * TraitFactor(worker.Trait, IsNight(hour));

        public static float EnergyDrain(StaffTrait trait) => EnergyDrainPerHour * (trait == StaffTrait.Tireless ? 0.6f : 1f);

        /// <summary>
        /// Tomorrow's mood: moves 30% of the way towards what the job is worth to them. Pay above what they asked
        /// cheers them up, being worn out and pay below it bring them down.
        /// </summary>
        public static float NextMood(float mood, float wage, float fairWage, bool exhausted)
        {
            float pay = fairWage > 0f ? wage / fairWage - 1f : 0f;
            float target = math.saturate(0.55f + pay * 1.5f - (exhausted ? 0.2f : 0f));
            return math.saturate(math.lerp(mood, target, 0.3f));
        }

        public static float TrainingCost(int training) => 300f * (training + 1);

        public static bool CanTrain(in Worker worker) => worker.Training < MaxTraining && worker.Skill < MaxSkill;

        public static float Raise(float wage) => math.round(wage * (1f + RaiseShare));

        /// <summary>Cashiers make shopping faster: one average cashier halves the time at the till.</summary>
        public static float ShopTimeFactor(float cashierPower) => 1f / (1f + 0.5f * cashierPower);
    }
}
