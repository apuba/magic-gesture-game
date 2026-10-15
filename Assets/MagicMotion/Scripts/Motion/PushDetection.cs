namespace MagicMotion.Motion
{
    public readonly struct PushDetection
    {
        public PushDetection(
            bool detected,
            float confidence,
            float velocity,
            float scaleGrowth,
            float depthChange,
            float lateralDelta,
            float palmWidth)
        {
            Detected = detected;
            Confidence = confidence;
            Velocity = velocity;
            ScaleGrowth = scaleGrowth;
            DepthChange = depthChange;
            LateralDelta = lateralDelta;
            PalmWidth = palmWidth;
        }

        public bool Detected { get; }
        public float Confidence { get; }
        public float Velocity { get; }
        public float ScaleGrowth { get; }
        public float DepthChange { get; }
        public float LateralDelta { get; }
        // Palm width at the start of the push: the value the distance-tiered
        // growth gate is chosen from, so it has to be visible in diagnostics.
        public float PalmWidth { get; }
    }
}
