namespace MagicMotion.Skill
{
    public static class SkillInputMatcher
    {
        public static bool Matches(SkillDefinition skill, SkillInputSnapshot input)
        {
            if (skill == null ||
                input.Gesture != skill.Gesture ||
                input.Palm != skill.Palm ||
                input.Motion != skill.Motion ||
                input.GestureConfidence < skill.MinimumConfidence ||
                input.MotionConfidence < skill.MinimumConfidence)
            {
                return false;
            }

            var observationAge = input.MotionTimestamp - input.PalmTimestamp;
            return observationAge >= 0d && observationAge <= skill.InputMaxAge;
        }
    }
}

