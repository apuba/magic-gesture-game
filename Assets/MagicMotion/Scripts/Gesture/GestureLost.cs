using MagicMotion.Vision;

namespace MagicMotion.Gesture
{
    public readonly struct GestureLost
    {
        public GestureLost(GestureType gesture, Handedness hand, double timestamp)
        {
            Gesture = gesture;
            Hand = hand;
            Timestamp = timestamp;
        }

        public GestureType Gesture { get; }
        public Handedness Hand { get; }
        public double Timestamp { get; }
    }
}
