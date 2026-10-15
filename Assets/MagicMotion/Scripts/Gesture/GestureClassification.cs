namespace MagicMotion.Gesture
{
    public readonly struct GestureClassification
    {
        public GestureClassification(GestureType type, float confidence, FingerAnalysis fingers)
        {
            Type = type;
            Confidence = confidence;
            Fingers = fingers;
        }

        public GestureType Type { get; }
        public float Confidence { get; }
        public FingerAnalysis Fingers { get; }
    }
}

