using MagicMotion.Gesture;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class RuneStrokeTrackerTests
    {
        [Test]
        public void StableSwordFinger_StartsDrawingAfterHoverDuration()
        {
            var tracker = new RuneStrokeTracker(hoverSeconds: 1f);
            var point = new Vector3(0.4f, 0.6f);

            tracker.UpdatePose(true, point, 0.2f, 0d);
            tracker.UpdatePose(true, point, 0.2f, 0.9d);
            Assert.That(tracker.State, Is.EqualTo(RuneStrokeState.SwordFingerCandidate));

            tracker.UpdatePose(true, point, 0.2f, 1.01d);
            Assert.That(tracker.State, Is.EqualTo(RuneStrokeState.Drawing));
            Assert.That(tracker.Points.Count, Is.EqualTo(1));
        }

        [Test]
        public void MovingDuringHover_RestartsCandidateTimer()
        {
            var tracker = new RuneStrokeTracker(hoverSeconds: 1f, maximumHoverDrift: 0.3f);

            tracker.UpdatePose(true, new Vector3(0.2f, 0.2f), 0.2f, 0d);
            tracker.UpdatePose(true, new Vector3(0.3f, 0.2f), 0.2f, 0.8d);
            tracker.UpdatePose(true, new Vector3(0.3f, 0.2f), 0.2f, 1.1d);

            Assert.That(tracker.State, Is.EqualTo(RuneStrokeState.SwordFingerCandidate));
            Assert.That(tracker.CandidateProgress, Is.LessThan(0.5f));
        }

        [Test]
        public void Drawing_EndsOnlyAfterMissingGracePeriod()
        {
            var tracker = new RuneStrokeTracker(hoverSeconds: 0.1f, missingGraceSeconds: 0.2f);
            var point = new Vector3(0.4f, 0.6f);
            tracker.UpdatePose(true, point, 0.2f, 0d);
            tracker.UpdatePose(true, point, 0.2f, 0.11d);

            tracker.UpdatePose(false, Vector3.zero, 1f, 0.2d);
            Assert.That(tracker.State, Is.EqualTo(RuneStrokeState.Drawing));

            tracker.UpdatePose(false, Vector3.zero, 1f, 0.32d);
            Assert.That(tracker.State, Is.EqualTo(RuneStrokeState.StrokeEnd));
        }

        [Test]
        public void Drawing_AddsMovedBrushPoints()
        {
            var tracker = new RuneStrokeTracker(hoverSeconds: 0.1f);
            tracker.UpdatePose(true, new Vector3(0.4f, 0.6f), 0.2f, 0d);
            tracker.UpdatePose(true, new Vector3(0.4f, 0.6f), 0.2f, 0.11d);
            tracker.UpdatePose(true, new Vector3(0.45f, 0.65f), 0.2f, 0.14d);

            Assert.That(tracker.Points.Count, Is.EqualTo(2));
        }
    }
}
