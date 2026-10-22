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
            BreakBefore = false;
            FromTip = false;
        }

        public TimedTrailPoint(Vector3 position, double timestamp, bool breakBefore, bool fromTip)
        {
            Position = position;
            Timestamp = timestamp;
            BreakBefore = breakBefore;
            FromTip = fromTip;
        }

        public Vector3 Position { get; }
        public double Timestamp { get; }

        // Set on the first point after the trail switched anchor, so the old
        // segment can fade out on its own instead of being erased.
        public bool BreakBefore { get; }

        // True when the point was sampled from the index fingertip, false when
        // it came from the palm centre, so the trail can colour them apart.
        public bool FromTip { get; }
    }

    public sealed class TimedHandTrail
    {
        private float lifetimeSeconds;
        private readonly float minimumPointDistance;
        private readonly List<TimedTrailPoint> points = new List<TimedTrailPoint>(64);
        private bool breakPending;

        public TimedHandTrail(float lifetimeSeconds = 0.65f, float minimumPointDistance = 0.01f)
        {
            this.lifetimeSeconds = Mathf.Max(0.05f, lifetimeSeconds);
            this.minimumPointDistance = Mathf.Max(0.0001f, minimumPointDistance);
        }

        public IReadOnlyList<TimedTrailPoint> Points => points;

        public void MarkBreak()
        {
            breakPending = true;
        }

        public void Clear()
        {
            points.Clear();
        }

        public float LifetimeSeconds
        {
            get { return lifetimeSeconds; }
            set { lifetimeSeconds = Mathf.Max(0.05f, value); }
        }

        public void Add(Vector3 position, double timestamp, bool fromTip = false)
        {
            Prune(timestamp);
            if (points.Count == 0 ||
                Vector3.Distance(points[points.Count - 1].Position, position) >= minimumPointDistance)
            {
                points.Add(new TimedTrailPoint(position, timestamp, breakPending, fromTip));
                breakPending = false;
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
