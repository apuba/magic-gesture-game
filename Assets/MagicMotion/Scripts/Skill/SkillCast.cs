using MagicMotion.Vision;

namespace MagicMotion.Skill
{
    public readonly struct SkillCast
    {
        public SkillCast(string skillId, string skillName, string vfx, Handedness hand, double timestamp)
        {
            SkillId = skillId;
            SkillName = skillName;
            Vfx = vfx;
            Hand = hand;
            Timestamp = timestamp;
        }

        public string SkillId { get; }
        public string SkillName { get; }
        public string Vfx { get; }
        public Handedness Hand { get; }
        public double Timestamp { get; }
    }
}

