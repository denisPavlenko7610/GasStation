using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;

namespace GasStation.Bridge
{
    /// <summary>Localized names for game data. Strings live in LocTable / the "GasStation" string table.</summary>
    public static class GameTexts
    {
        public static string FuelName(FuelType type) => Loc.T($"fuel.{type}");

        public static string ProductName(ProductType type) => Loc.T($"product.{type}");

        public static string UpgradeName(UpgradeType type) => Loc.T($"upgrade.name.{type}");

        /// <param name="id">1-based regular id.</param>
        public static string RegularName(int id) => Loc.T($"regular.{id - 1}.name");

        public static string RegularAbout(int id) => Loc.T($"regular.{id - 1}.about");

        public static string ContractName(ContractType type) => Loc.T($"contract.{type}.name");

        /// <summary>★★★☆☆ for 0..5 stars.</summary>
        public static string Stars(int stars)
        {
            int full = stars < 0 ? 0 : stars > 5 ? 5 : stars;
            return new string('★', full) + new string('☆', 5 - full);
        }

        /// <summary>♥♥♥♡♡ for loyalty 0..1.</summary>
        public static string Hearts(float loyalty) => Stars((int)System.Math.Round(loyalty * 5f)).Replace('★', '♥').Replace('☆', '♡');

        public static string TraitName(StaffTrait trait) => Loc.T($"trait.{trait}");

        public static string TraitDescription(StaffTrait trait) => Loc.T($"trait.{trait}.desc");

        /// <summary>Mood in words: miserable, unhappy, okay, happy.</summary>
        public static string MoodText(float mood) => Loc.T(mood < 0.2f ? "mood.0" : mood < 0.45f ? "mood.1" : mood < 0.7f ? "mood.2" : "mood.3");

        public static string PropName(PropType type) => Loc.T($"prop.name.{type}");

        public static string PropDescription(PropType type) => Loc.T($"prop.desc.{type}");

        public static string UpgradeDescription(UpgradeType type) => Loc.T($"upgrade.desc.{type}");

        public static string StaffName(int index)
        {
            int count = StaffMath.NameCount;
            return Loc.T($"staff.name.{((index % count) + count) % count}");
        }

        public static string RoleName(StaffRole role) => Loc.T($"role.{role}");

        public static string RoleDuty(StaffRole role) => Loc.T($"duty.{role}");

        public static string ReferenceText(int grade) => Loc.T($"reference.{grade}");

        public static string SchemeName(int scheme) => Loc.T($"scheme.{scheme}");

        public static string CustomerName(CustomerType type) => Loc.T($"customer.{type}");

        public static string CarStateName(CarState state) => Loc.T($"carstate.{state}");

        public static string QuestTitle(QuestDefinition quest) =>
            quest.IsDaily ? Loc.T("quest.title.daily") : Loc.T($"quest.title.{quest.Id}");

        /// <summary>Goal line with the current progress filled in.</summary>
        public static string QuestGoalText(QuestDefinition quest, float progress) =>
            Loc.F($"quest.goal.{quest.Goal}", progress.ToString("0"), quest.Target);

        public static string QuestReward(QuestDefinition quest)
        {
            float reputation = quest.RewardReputation * 100f;
            if (quest.RewardMoney > 0f && quest.RewardReputation > 0f)
                return Loc.F("quest.reward.both", quest.RewardMoney, reputation);
            return quest.RewardMoney > 0f
                ? Loc.F("quest.reward.money", quest.RewardMoney)
                : Loc.F("quest.reward.reputation", reputation);
        }
    }
}
