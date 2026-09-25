using GasStation.Components;

namespace GasStation.Logic
{
    public struct ChapterGoal
    {
        /// <summary>Last day to reach the goal (inclusive).</summary>
        public int Deadline;
        /// <summary>Total repaid by then.</summary>
        public float Repaid;
        public int Level;
    }

    /// <summary>"Inheritance": repay $50 000 in 30 days, in three chapters.</summary>
    public static class CampaignMath
    {
        public const float Debt = 50000f;
        public const int Chapters = 3;
        /// <summary>What PetroMax pays for the land in chapter two.</summary>
        public const float BuyoutOffer = 30000f;
        public const float SandboxMoney = 25000f;

        public static ChapterGoal Goal(int chapter) => chapter switch
        {
            // Early on the station earns roughly $700 a day; the goals grow with it.
            1 => new ChapterGoal { Deadline = 10, Repaid = 5000f, Level = 3 },
            2 => new ChapterGoal { Deadline = 20, Repaid = 20000f, Level = 1 },
            _ => new ChapterGoal { Deadline = 30, Repaid = Debt, Level = 1 }
        };

        public static bool Met(ChapterGoal goal, float repaid, int level) => repaid >= goal.Repaid - 0.01f && level >= goal.Level;

        /// <summary>The deadline day is over when the calendar shows a later day.</summary>
        public static bool Missed(ChapterGoal goal, int day) => day > goal.Deadline;

        public static float Remaining(float repaid) => Debt - repaid > 0f ? Debt - repaid : 0f;

        /// <summary>A payment can never exceed what is still owed.</summary>
        public static float Payment(float wanted, float repaid, float money)
        {
            float amount = wanted < Remaining(repaid) ? wanted : Remaining(repaid);
            return amount < money ? amount : money > 0f ? money : 0f;
        }
    }
}
