using System;

namespace MagicMotion.Vision
{
    public sealed class FrameRateGate
    {
        private readonly double interval;
        private double nextAllowedTime;
        private bool hasProcessedFrame;

        public FrameRateGate(int framesPerSecond)
        {
            if (framesPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            }

            interval = 1d / framesPerSecond;
        }

        public bool ShouldProcess(double timestamp)
        {
            if (!hasProcessedFrame || timestamp < nextAllowedTime - interval)
            {
                hasProcessedFrame = true;
                nextAllowedTime = timestamp + interval;
                return true;
            }

            if (timestamp < nextAllowedTime)
            {
                return false;
            }

            nextAllowedTime = timestamp + interval;
            return true;
        }

        public void Reset()
        {
            hasProcessedFrame = false;
            nextAllowedTime = 0d;
        }
    }
}

