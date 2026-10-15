using System;
using System.Collections.Generic;

namespace MagicMotion.Vision
{
    public readonly struct HandTrackingResult
    {
        public HandTrackingResult(long frameSequence, double timestamp, IReadOnlyList<HandData> hands)
        {
            FrameSequence = frameSequence;
            Timestamp = timestamp;
            Hands = hands ?? Array.Empty<HandData>();
        }

        public long FrameSequence { get; }
        public double Timestamp { get; }
        public IReadOnlyList<HandData> Hands { get; }
    }
}

