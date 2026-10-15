using System;
using MagicMotion.Config;
using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Vision;

namespace MagicMotion.Skill
{
    public sealed class SkillDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public GestureType Gesture { get; private set; }
        public PalmDirection Palm { get; private set; }
        public MotionType Motion { get; private set; }
        public float MinimumConfidence { get; private set; }
        public float InputMaxAge { get; private set; }
        public float Cooldown { get; private set; }
        public string Vfx { get; private set; }

        public static SkillDefinition FromConfig(SkillEntryConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.id))
            {
                throw new ArgumentException("Every skill requires an id.", nameof(config));
            }

            if (!Enum.TryParse(config.gesture, true, out GestureType gesture) || gesture == GestureType.Unknown)
            {
                throw new ArgumentException($"Skill {config.id} has an invalid gesture: {config.gesture}");
            }

            if (!Enum.TryParse(config.palm, true, out PalmDirection palm) || palm == PalmDirection.Unknown)
            {
                throw new ArgumentException($"Skill {config.id} has an invalid palm direction: {config.palm}");
            }

            if (!Enum.TryParse(config.motion, true, out MotionType motion) || motion == MotionType.Unknown)
            {
                throw new ArgumentException($"Skill {config.id} has an invalid motion: {config.motion}");
            }

            if (config.minimumConfidence <= 0f || config.minimumConfidence > 1f ||
                config.inputMaxAge <= 0f || config.cooldown < 0f)
            {
                throw new ArgumentException($"Skill {config.id} contains invalid thresholds.");
            }

            return new SkillDefinition
            {
                Id = config.id.Trim(),
                Name = config.name,
                Gesture = gesture,
                Palm = palm,
                Motion = motion,
                MinimumConfidence = config.minimumConfidence,
                InputMaxAge = config.inputMaxAge,
                Cooldown = config.cooldown,
                Vfx = config.vfx
            };
        }
    }
}

