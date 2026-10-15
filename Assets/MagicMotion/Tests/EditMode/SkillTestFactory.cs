using MagicMotion.Config;
using MagicMotion.Skill;

namespace MagicMotion.Tests
{
    internal static class SkillTestFactory
    {
        public static SkillDefinition PalmStrike(float cooldown = 1f)
        {
            return SkillDefinition.FromConfig(new SkillEntryConfig
            {
                id = "SK001",
                name = "震山掌",
                gesture = "OpenPalm",
                palm = "Forward",
                motion = "Push",
                minimumConfidence = 0.85f,
                inputMaxAge = 0.25f,
                cooldown = cooldown,
                vfx = "PalmStrike"
            });
        }
    }
}

