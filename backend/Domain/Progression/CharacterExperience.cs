using Domain.Entities;
using Domain.Enums;

namespace Domain.Progression
{
    public static class CharacterExperience
    {
        public const int BaseExperienceForLevel = 100;
        public const int ExperienceGrowthPerLevel = 50;

        public static int GetTaskExperience(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 15,
                Difficulty.Medium => 30,
                Difficulty.Hard => 50,
                _ => 30
            };
        }

        public static ExperienceState BuildState(int totalExperience)
        {
            var safeTotal = Math.Max(0, totalExperience);
            var level = 1;
            var experienceInLevel = safeTotal;
            var experienceToNext = GetExperienceToNextLevel(level);

            while (experienceInLevel >= experienceToNext)
            {
                experienceInLevel -= experienceToNext;
                level += 1;
                experienceToNext = GetExperienceToNextLevel(level);
            }

            return new ExperienceState(
                level,
                safeTotal,
                experienceInLevel,
                experienceToNext);
        }

        public static void ApplyExperience(CharacterProfile profile, int delta)
        {
            var nextTotal = Math.Max(0, profile.TotalExperience + delta);
            var state = BuildState(nextTotal);

            profile.TotalExperience = state.TotalExperience;
            profile.Level = state.Level;
        }

        public static int GetExperienceToNextLevel(int level)
        {
            var safeLevel = Math.Max(1, level);
            return BaseExperienceForLevel + (safeLevel - 1) * ExperienceGrowthPerLevel;
        }
    }

    public readonly record struct ExperienceState(
        int Level,
        int TotalExperience,
        int ExperienceInLevel,
        int ExperienceToNextLevel);
}
