using MagicMotion.Vision;

namespace MagicMotion.Skill
{
    public readonly struct SkillStateChanged
    {
        public SkillStateChanged(string skillId, Handedness hand, SkillState state, double timestamp)
        {
            SkillId = skillId;
            Hand = hand;
            State = state;
            Timestamp = timestamp;
        }

        public string SkillId { get; }
        public Handedness Hand { get; }
        public SkillState State { get; }
        public double Timestamp { get; }
    }
}

