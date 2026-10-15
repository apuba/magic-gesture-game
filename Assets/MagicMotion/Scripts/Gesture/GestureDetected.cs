using MagicMotion.Vision;

namespace MagicMotion.Gesture
{
    public readonly struct GestureDetected
    {
        public GestureDetected(
            GestureType gesture,
            Handedness hand,
            float confidence,
            double timestamp)
        {
            Gesture = gesture;
            Hand = hand;
            Confidence = confidence;
            Timestamp = timestamp;
        }

        public GestureType Gesture { get; }
        public Handedness Hand { get; }
        public float Confidence { get; }
        public double Timestamp { get; }
    }
}

