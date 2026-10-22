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
    }
}
