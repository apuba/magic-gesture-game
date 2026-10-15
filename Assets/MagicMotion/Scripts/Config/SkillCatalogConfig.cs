using System;

namespace MagicMotion.Config
{
    [Serializable]
    public sealed class SkillCatalogConfig
    {
        public SkillEntryConfig[] skills = Array.Empty<SkillEntryConfig>();
    }

    [Serializable]
    public sealed class SkillEntryConfig
    {
        public string id;
        public string name;
        public string gesture;
        public string palm;
        public string motion;
        // Kept in step with Resources/Config/Skills.json. Lowered from 0.85:
        // measured pushes of ordinary strength scored 0.79~0.84 and were
        // detected and then silently dropped by the skill layer.
        public float minimumConfidence = 0.78f;
        public float inputMaxAge = 0.25f;
        public float cooldown = 1.0f;
        public string vfx;
    }
}
