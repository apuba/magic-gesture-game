using MagicMotion.UI;
using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests.EditMode
{
    public sealed class RuneFollowTrackerTests
    {
        [Test]
        public void StableSwordPoseShowsRuneAndUpdatesTarget()
        {
            var tracker = new RuneFollowTracker(0.2d, 0.18d);
            tracker.UpdatePose(true, new Vector3(0.2f, 0.3f), 1d);
            Assert.That(tracker.Visible, Is.False);

            tracker.UpdatePose(true, new Vector3(0.4f, 0.5f), 1.21d);

            Assert.That(tracker.Visible, Is.True);
            Assert.That(tracker.Target, Is.EqualTo(new Vector3(0.4f, 0.5f)));
        }

        [Test]
        public void BriefPoseLossDoesNotFlashRuneAway()
        {
            var tracker = new RuneFollowTracker(0d, 0.18d);
            tracker.UpdatePose(true, Vector3.one, 1d);

            tracker.UpdatePose(false, Vector3.zero, 1.1d);
            Assert.That(tracker.Visible, Is.True);

            tracker.UpdatePose(false, Vector3.zero, 1.19d);
            Assert.That(tracker.Visible, Is.False);
        }

        [Test]
        public void TargetSitsBeyondTheJoinedFingertips()
        {
            var hand = GestureHandFactory.SwordFinger();
            var indexTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip);
            var middleTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleTip);
            var tips = (indexTip + middleTip) * 0.5f;

            var target = RuneFollowTracker.CalculateTarget(hand);

            Assert.That(Vector2.Distance(target, tips), Is.GreaterThan(0f));
            Assert.That(target.y, Is.GreaterThan(tips.y));
        }
    }
}
