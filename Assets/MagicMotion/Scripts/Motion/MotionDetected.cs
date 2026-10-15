using MagicMotion.Vision;

namespace MagicMotion.Motion
{
    public readonly struct MotionDetected
    {
        public MotionDetected(
            MotionType motion,
            Handedness hand,
            float confidence,
            float velocity,
            double timestamp)
        {
            Motion = motion;
            Hand = hand;
            Confidence = confidence;
            Velocity = velocity;
            Timestamp = timestamp;
        }

        public MotionType Motion { get; }
        public Handedness Hand { get; }
        public float Confidence { get; }
        public float Velocity { get; }
        public double Timestamp { get; }
    }
}

