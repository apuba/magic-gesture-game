using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Vision;

namespace MagicMotion.Skill
{
    public readonly struct SkillInputSnapshot
    {
        public SkillInputSnapshot(
            Handedness hand,
            GestureType gesture,
            float gestureConfidence,
            PalmDirection palm,
            double palmTimestamp,
            MotionType motion,
            float motionConfidence,
            double motionTimestamp)
        {
            Hand = hand;
            Gesture = gesture;
            GestureConfidence = gestureConfidence;
            Palm = palm;
            PalmTimestamp = palmTimestamp;
            Motion = motion;
            MotionConfidence = motionConfidence;
            MotionTimestamp = motionTimestamp;
        }

        public Handedness Hand { get; }
        public GestureType Gesture { get; }
        public float GestureConfidence { get; }
        public PalmDirection Palm { get; }
        public double PalmTimestamp { get; }
        public MotionType Motion { get; }
        public float MotionConfidence { get; }
        public double MotionTimestamp { get; }
    }
}

