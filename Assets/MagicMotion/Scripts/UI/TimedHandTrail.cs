using System.Collections.Generic;
using UnityEngine;

namespace MagicMotion.UI
{
    public readonly struct TimedTrailPoint
    {
        public TimedTrailPoint(Vector3 position, double timestamp)
        {
            Position = position;
            Timestamp = timestamp;
        }

        public Vector3 Position { get; }
        public double Timestamp { get; }
    }

    public sealed class TimedHandTrail
    {
        private readonly float lifetimeSeconds;
        private readonly float minimumPointDistance;
        private readonly List<TimedTrailPoint> points = new List<TimedTrailPoint>(64);

        public TimedHandTrail(float lifetimeSeconds = 0.65f, float minimumPointDistance = 0.01f)
        {
            this.lifetimeSeconds = Mathf.Max(0.05f, lifetimeSeconds);
            this.minimumPointDistance = Mathf.Max(0.0001f, minimumPointDistance);
        }

        public IReadOnlyList<TimedTrailPoint> Points => points;

        public void Add(Vector3 position, double timestamp)
        {
            Prune(timestamp);
            if (points.Count == 0 ||
                Vector3.Distance(points[points.Count - 1].Position, position) >= minimumPointDistance)
            {
                points.Add(new TimedTrailPoint(position, timestamp));
            }
        }

        public void Prune(double timestamp)
        {
            var firstAlive = 0;
            while (firstAlive < points.Count && timestamp - points[firstAlive].Timestamp > lifetimeSeconds)
            {
                firstAlive++;
            }

            if (firstAlive > 0)
            {
                points.RemoveRange(0, firstAlive);
            }
        }

        public float Alpha(TimedTrailPoint point, double timestamp)
        {
            return 1f - Mathf.Clamp01((float)((timestamp - point.Timestamp) / lifetimeSeconds));
        }
    }
}
