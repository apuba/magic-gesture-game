using MagicMotion.Config;
using MagicMotion.Motion;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class PushMotionTrackerTests
    {
        private static MotionConfig Settings => new MotionConfig
        {
            minimumDuration = 0.1f,
            maximumDuration = 0.45f,
            minimumScaleGrowth = 0.18f,
            minimumScaleVelocity = 0.25f,
            minimumDepthChange = 0.04f,
            maximumLateralDrift = 0.75f,
            cooldownSeconds = 0.6f,
            assumedAxialShare = 0.5f,
            minimumRetractRatio = 0.03f
        };

        [Test]
        public void Add_DetectsForwardPushUsingScaleAndDepth()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.0f, 0.00f, Vector3.zero));

            var result = tracker.Add(Sample(0.3, 1.3f, -0.08f, new Vector3(0.05f, 0.03f)));

            Assert.That(result.Detected, Is.True);
            Assert.That(result.Velocity, Is.GreaterThan(0.25f));
        }

        [Test]
        public void Add_RejectsSidewaysMovement()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.0f, 0.00f, Vector3.zero));

            var result = tracker.Add(Sample(0.3, 1.3f, -0.08f, new Vector3(1.0f, 0f)));

            Assert.That(result.Detected, Is.False);
        }

        [Test]
        public void Add_ScaleOnlyPushCanReachHighConfidence()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.0f, 0.00f, Vector3.zero));

            var result = tracker.Add(Sample(0.3, 1.5f, 0.00f, Vector3.zero));

            Assert.That(result.Detected, Is.True);
            Assert.That(result.Confidence, Is.GreaterThanOrEqualTo(0.85f));
        }

        [Test]
        public void Add_RejectsStationaryHand()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.0f, 0.00f, Vector3.zero));

            var result = tracker.Add(Sample(0.3, 1.0f, 0.00f, Vector3.zero));

            Assert.That(result.Detected, Is.False);
        }

        [Test]
        public void Add_EnforcesCooldown()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.0f, 0.00f, Vector3.zero));
            Assert.That(tracker.Add(Sample(0.3, 1.3f, -0.08f, Vector3.zero)).Detected, Is.True);

            var result = tracker.Add(Sample(0.5, 1.7f, -0.16f, Vector3.zero));

            Assert.That(result.Detected, Is.False);
        }

        [Test]
        public void Add_RejectsSlowApproach()
        {
            var tracker = new PushMotionTracker(Settings);
            // The hand creeps towards the lens. Total growth is large, but no
            // burst window sees enough of it, so this is not a push.
            tracker.Add(Sample(0.0, 1.00f, 0.00f, Vector3.zero));
            for (var step = 1; step <= 14; step++)
            {
                tracker.Add(Sample(step * 0.1, 1f + step * 0.03f, -0.004f * step, Vector3.zero));
            }

            var result = tracker.Add(Sample(1.5, 1.45f, -0.06f, Vector3.zero));

            Assert.That(result.Detected, Is.False);
        }

        [Test]
        public void Add_RejectsRadialFlingAwayFromCentre()
        {
            var tracker = new PushMotionTracker(Settings);
            // Palm flies diagonally outwards with enough growth to qualify. The
            // outward travel is far larger than any perspective drift the growth
            // could explain, so it counts as drift now that only the predicted
            // share is forgiven.
            tracker.Add(Sample(0.0, 1.00f, 0.00f, new Vector3(0.15f, 0.15f)));

            var result = tracker.Add(Sample(0.3, 1.25f, -0.05f, new Vector3(0.85f, 0.85f)));

            Assert.That(result.Detected, Is.False);
        }

        [Test]
        public void Add_ToleratesPerspectiveDriftFromCentredPush()
        {
            var tracker = new PushMotionTracker(Settings);
            // A genuine push starting off centre drifts outwards by (offset x
            // growth) when aimed along the optical axis. That has to stay usable
            // wherever the hand sits in the frame.
            tracker.Add(Sample(0.0, 1.00f, 0.00f, new Vector3(0.20f, 0.20f)));

            var result = tracker.Add(Sample(0.3, 1.30f, -0.08f, new Vector3(0.26f, 0.26f)));

            Assert.That(result.Detected, Is.True);
        }

        [Test]
        public void Add_RequiresRetractBeforeNextDetection()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(0.0, 1.00f, 0.00f, Vector3.zero));
            Assert.That(tracker.Add(Sample(0.3, 1.40f, -0.10f, Vector3.zero)).Detected, Is.True);

            // Same approach still going, well past the cooldown: must stay silent.
            var ongoing = tracker.Add(Sample(1.2, 1.80f, -0.20f, Vector3.zero));
            Assert.That(ongoing.Detected, Is.False);

            tracker.Add(Sample(1.4, 1.30f, -0.16f, Vector3.zero));
            var afterRetract = tracker.Add(Sample(1.7, 2.00f, -0.30f, Vector3.zero));
            Assert.That(afterRetract.Detected, Is.True);
        }

        [Test]
        public void Add_ResetsWhenTimestampMovesBackward()
        {
            var tracker = new PushMotionTracker(Settings);
            tracker.Add(Sample(5.0, 1.0f, 0.00f, Vector3.zero));

            var result = tracker.Add(Sample(1.0, 1.3f, -0.08f, Vector3.zero));

            Assert.That(result.Detected, Is.False);
        }

        private static HandMotionSample Sample(
            double timestamp,
            float width,
            float depth,
            Vector3 center)
        {
            return new HandMotionSample(timestamp, center, width, depth);
        }
    }
}
