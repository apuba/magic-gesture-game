using UnityEngine;

namespace MagicMotion.Motion
{
    public readonly struct HandMotionSample
    {
        public HandMotionSample(
            double timestamp,
            Vector3 palmCenter,
            float palmWidth,
            float meanDepth)
        {
            Timestamp = timestamp;
            PalmCenter = palmCenter;
            PalmWidth = palmWidth;
            MeanDepth = meanDepth;
        }

        public double Timestamp { get; }
        public Vector3 PalmCenter { get; }
        public float PalmWidth { get; }
        public float MeanDepth { get; }
    }
}

