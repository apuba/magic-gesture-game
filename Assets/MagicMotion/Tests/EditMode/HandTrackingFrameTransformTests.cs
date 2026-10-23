using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class HandTrackingFrameTransformTests
    {
        // Measured on the reference phone: portrait (270) runs on
        // rotationDegrees alone, so no orientation may add a vertical flip on
        // top of that one rotation. Flipping 0 and 180 upside down turned both
        // landscape orientations over and lost a still hand almost every frame.
        [TestCase(0, true, false)]
        [TestCase(90, false, false)]
        [TestCase(180, true, false)]
        [TestCase(270, false, false)]
        public void FrontCameraInputFlips_KeepTheFrameUpright(int rotation,
            bool expectedHorizontal, bool expectedVertical)
        {
            HandTrackingFrameTransform.GetMediaPipeInputFlips(
                true, false, rotation, out var horizontal, out var vertical);
            Assert.That(horizontal, Is.EqualTo(expectedHorizontal));
            Assert.That(vertical, Is.EqualTo(expectedVertical));
        }

        [TestCase(0, 0.2f, 0.75f)]
        [TestCase(90, 0.75f, 0.2f)]
        [TestCase(180, 0.8f, 0.25f)]
        [TestCase(270, 0.25f, 0.8f)]
        public void FrontCameraLandmarkMapping_CoversEveryRightAngle(int rotation,
            float expectedX, float expectedY)
        {
            var mapped = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                new Vector3(0.2f, 0.75f, 0f), rotation, true, false);
            Assert.That(mapped.x, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(mapped.y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        // A frame that is already reported vertically mirrored gets flipped
        // again by GetDisplayUv, so the two flips cancel and the landmark
        // keeps the tracker's own vertical axis.
        [Test]
        public void VerticallyMirroredFrame_CancelsTheInherentFlip()
        {
            var mapped = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                new Vector3(0.2f, 0.75f, 0f), 0, true, true);
            Assert.That(mapped.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(mapped.y, Is.EqualTo(0.25f).Within(0.0001f));
        }

        // The landscape orientation that reports 180 draws the picture turned
        // half a turn, so its skeleton has to be turned half a turn as well.
        // Collapsing it onto 0 left the skeleton upside down on that side.
        [Test]
        public void HalfTurnLandscape_IsHalfTurnOfUprightLandscape()
        {
            var upright = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                new Vector3(0.2f, 0.75f, 0f), 0, true, false);
            var halfTurn = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                new Vector3(0.2f, 0.75f, 0f), 180, true, false);
            Assert.That(halfTurn.x, Is.EqualTo(1f - upright.x).Within(0.0001f));
            Assert.That(halfTurn.y, Is.EqualTo(1f - upright.y).Within(0.0001f));
        }
    }
}
