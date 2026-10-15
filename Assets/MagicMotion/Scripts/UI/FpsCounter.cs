using System;

namespace MagicMotion.UI
{
    public sealed class FpsCounter
    {
        private readonly double updateInterval;
        private double elapsed;
        private int frames;

        public FpsCounter(double updateInterval = 0.5d)
        {
            if (updateInterval <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(updateInterval));
            }

            this.updateInterval = updateInterval;
        }

        public float Current { get; private set; }

        public bool AddFrame(double deltaTime)
        {
            if (deltaTime < 0d)
            {
                return false;
            }

            elapsed += deltaTime;
            frames++;
            if (elapsed < updateInterval)
            {
                return false;
            }

            Current = elapsed > 0d ? (float)(frames / elapsed) : 0f;
            elapsed = 0d;
            frames = 0;
            return true;
        }
    }
}

