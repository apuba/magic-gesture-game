using MagicMotion.UI;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class TimedHandTrailTests
    {
        [Test]
        public void Add_IgnoresJitterBelowMinimumDistance()
        {
            var trail = new TimedHandTrail(minimumPointDistance: 0.01f);
            trail.Add(Vector3.zero, 0d);
            trail.Add(new Vector3(0.005f, 0f), 0.02d);

            Assert.That(trail.Points.Count, Is.EqualTo(1));
        }

        [Test]
        public void Prune_RemovesPointsOlderThanLifetime()
        {
            var trail = new TimedHandTrail(lifetimeSeconds: 0.5f);
            trail.Add(Vector3.zero, 0d);
            trail.Add(new Vector3(0.1f, 0f), 0.25d);

            trail.Prune(0.6d);

            Assert.That(trail.Points.Count, Is.EqualTo(1));
            Assert.That(trail.Points[0].Position.x, Is.EqualTo(0.1f));
        }

        // A quick diagonal leaves only a few samples, spaced unevenly, and the
        // jitter in them is what turns the stroke into a zigzag. Filling the
        // gaps must not bend a straight stroke on its own account.
        [Test]
        public void Sample_KeepsAnUnevenlySpacedStraightStrokeStraight()
        {
            var before = new Vector2(0f, 0f);
            var from = new Vector2(0.4f, 0.4f);
            var to = new Vector2(1.5f, 1.5f);
            var after = new Vector2(3f, 3f);

            for (var step = 1; step < 8; step++)
            {
                var point = TrailCurve.Sample(before, from, to, after, step / 8f);
                Assert.That(point.y - point.x, Is.EqualTo(0f).Within(0.0001f));
            }
        }

        // The same case for an arc: a quarter circle drawn fast leaves four
        // samples, and joining those reads as a polygon.
        [Test]
        public void Sample_FollowsAnArcCloserThanAStraightSegment()
        {
            var samples = new Vector2[4];
            for (var index = 0; index < samples.Length; index++)
            {
                var angle = index / (samples.Length - 1f) * 0.5f * Mathf.PI;
                samples[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            var from = samples[1];
            var to = samples[2];
            var straightError = 0f;
            var curveError = 0f;
            for (var step = 0; step <= 64; step++)
            {
                var t = step / 64f;
                straightError = Mathf.Max(
                    straightError, Mathf.Abs(Vector2.Lerp(from, to, t).magnitude - 1f));
                curveError = Mathf.Max(curveError,
                    Mathf.Abs(TrailCurve.Sample(samples[0], from, to, samples[3], t).magnitude - 1f));
            }

            Assert.That(curveError, Is.LessThan(straightError * 0.25f));
        }
    }
}
