namespace MagicMotion.Gesture
{
    public readonly struct FingerAnalysis
    {
        public FingerAnalysis(
            FingerState thumb,
            FingerState index,
            FingerState middle,
            FingerState ring,
            FingerState pinky)
        {
            Thumb = thumb;
            Index = index;
            Middle = middle;
            Ring = ring;
            Pinky = pinky;
        }

        public FingerState Thumb { get; }
        public FingerState Index { get; }
        public FingerState Middle { get; }
        public FingerState Ring { get; }
        public FingerState Pinky { get; }

        public int ExtendedCount =>
            Count(Thumb, FingerState.Extended) +
            Count(Index, FingerState.Extended) +
            Count(Middle, FingerState.Extended) +
            Count(Ring, FingerState.Extended) +
            Count(Pinky, FingerState.Extended);

        public bool LongFingersExtended =>
            Index == FingerState.Extended &&
            Middle == FingerState.Extended &&
            Ring == FingerState.Extended &&
            Pinky == FingerState.Extended;

        private static int Count(FingerState value, FingerState expected)
        {
            return value == expected ? 1 : 0;
        }
    }
}

