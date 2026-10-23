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

    /// <summary>
    /// Fills in the curve between two sampled points of a trail.
    ///
    /// A quick stroke leaves the samples far apart, and joining them with
    /// straight segments puts a visible fold at every sample: an arc reads as
    /// a polygon and a diagonal reads as a zigzag. This draws the arc instead.
    /// It is a Catmull-Rom spline, which passes through every sample rather
    /// than being pulled towards a smoothed average of them, so the curve
    /// never lags behind the hand the way a moving average would, and a corner
    /// the player actually drew stays a corner.
    /// </summary>
    public static class TrailCurve
    {
        // Centripetal: the knots go as the square root of the distance rather
        // than the distance itself, which is what keeps the curve from looping
        // back on itself or overshooting where the samples are unevenly spaced,
        // exactly as a fast stroke spaces them.
        private const float Alpha = 0.5f;

        public static Vector2 Sample(Vector2 before, Vector2 from, Vector2 to, Vector2 after, float t)
        {
            var spanTo = Knot(from, to);
            if (spanTo <= 0f)
            {
                return Vector2.Lerp(from, to, t);
            }

            // Hermite form: the tangents are the secants across the neighbouring
            // spans, scaled to the length of this one.
            var tangentFrom = (to - before) / Mathf.Max(Knot(before, from) + spanTo, 0.00001f);
            var tangentTo = (after - from) / Mathf.Max(spanTo + Knot(to, after), 0.00001f);

            var squared = t * t;
            var cubed = squared * t;
            return
                (2f * cubed - 3f * squared + 1f) * from +
                (cubed - 2f * squared + t) * spanTo * tangentFrom +
                (-2f * cubed + 3f * squared) * to +
                (cubed - squared) * spanTo * tangentTo;
        }

        private static float Knot(Vector2 a, Vector2 b)
        {
            return Mathf.Pow(Vector2.Distance(a, b), Alpha);
        }
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
