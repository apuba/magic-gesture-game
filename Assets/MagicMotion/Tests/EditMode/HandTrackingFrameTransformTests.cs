using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class HandTrackingFrameTransformTests
    {
        [TestCase(0, true, true)]
        [TestCase(90, false, false)]
        [TestCase(180, true, true)]
        [TestCase(270, false, false)]
        public void FrontCameraInputFlips_MatchMediaPipePlugin(int rotation,
            bool expectedHorizontal, bool expectedVertical)
        {
            HandTrackingFrameTransform.GetMediaPipeInputFlips(
                true, false, rotation, out var horizontal, out var vertical);
            Assert.That(horizontal, Is.EqualTo(expectedHorizontal));
            Assert.That(vertical, Is.EqualTo(expectedVertical));
        }

        [TestCase(0, 0.8f, 0.25f)]
        [TestCase(90, 0.25f, 0.2f)]
        [TestCase(180, 0.2f, 0.75f)]
        [TestCase(270, 0.75f, 0.8f)]
        public void FrontCameraLandmarkMapping_CoversEveryRightAngle(int rotation,
            float expectedX, float expectedY)
        {
            var mapped = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                new Vector3(0.2f, 0.75f, 0f), rotation, true);
            Assert.That(mapped.x, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(mapped.y, Is.EqualTo(expectedY).Within(0.0001f));
        }
    }
}
