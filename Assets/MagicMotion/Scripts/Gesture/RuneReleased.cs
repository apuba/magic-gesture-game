using MagicMotion.Vision;

namespace MagicMotion.Gesture
{
    /// <summary>
    /// Published when an armed talisman is released by a real push.
    /// </summary>
    public readonly struct RuneReleased
    {
        public RuneReleased(RuneElement element, Handedness hand, double timestamp)
        {
            Element = element;
            Hand = hand;
            Timestamp = timestamp;
        }

        public RuneElement Element { get; }
        public Handedness Hand { get; }
        public double Timestamp { get; }
    }
}
