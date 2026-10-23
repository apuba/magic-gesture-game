using System;
using System.Collections.Generic;

namespace MagicMotion.Vision
{
    public readonly struct HandTrackingResult
    {
        public HandTrackingResult(
            long frameSequence, double timestamp, IReadOnlyList<HandData> hands, float widthToHeight)
        {
            FrameSequence = frameSequence;
            Timestamp = timestamp;
            Hands = hands ?? Array.Empty<HandData>();
            WidthToHeight = widthToHeight > 0f ? widthToHeight : 16f / 9f;
        }

        public long FrameSequence { get; }
        public double Timestamp { get; }
        public IReadOnlyList<HandData> Hands { get; }

        /// <summary>
        /// Shape of the frame the landmarks were taken on. Anything that
        /// measures an angle or compares two distances needs it, because
        /// normalized coordinates are not square and the same hand measures
        /// differently lying one way or the other in the frame.
        /// </summary>
        public float WidthToHeight { get; }
    }
}

