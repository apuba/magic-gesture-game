using MagicMotion.UI;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests.EditMode
{
    public sealed class HandTrackingOverlayLayoutTests
    {
        [Test]
        public void FitPreservesLandscapeAspectInsidePortraitViewport()
        {
            var result = HandTrackingOverlayLayout.Fit(new Rect(0f, 0f, 1080f, 1920f), 1280, 720, 0);

            Assert.That(result.width, Is.EqualTo(1080f).Within(0.01f));
            Assert.That(result.height, Is.EqualTo(607.5f).Within(0.01f));
            Assert.That(result.center.x, Is.EqualTo(540f).Within(0.01f));
            Assert.That(result.center.y, Is.EqualTo(960f).Within(0.01f));
        }

        [Test]
        public void FitSwapsSourceDimensionsForQuarterTurn()
        {
            var result = HandTrackingOverlayLayout.Fit(new Rect(0f, 0f, 1080f, 1920f), 1280, 720, 90);

            Assert.That(result.width, Is.EqualTo(1080f).Within(0.01f));
            Assert.That(result.height, Is.EqualTo(1920f).Within(0.01f));
        }

        [Test]
        public void NormalizedCoordinatesMapToGuiWithInvertedYAxis()
        {
            var content = new Rect(100f, 200f, 400f, 800f);

            AssertPoint(HandTrackingOverlayLayout.NormalizedToGui(content, new Vector3(0f, 0f)), 100f, 1000f);
            AssertPoint(HandTrackingOverlayLayout.NormalizedToGui(content, new Vector3(1f, 1f)), 500f, 200f);
            AssertPoint(HandTrackingOverlayLayout.NormalizedToGui(content, new Vector3(0.5f, 0.25f)), 300f, 800f);
        }

        [TestCase(-90, 270)]
        [TestCase(44, 0)]
        [TestCase(46, 90)]
        [TestCase(181, 180)]
        [TestCase(359, 0)]
        public void RotationIsNormalizedToRightAngles(int input, int expected)
        {
            Assert.That(HandTrackingOverlayLayout.NormalizeRotation(input), Is.EqualTo(expected));
        }

        private static void AssertPoint(Vector2 actual, float expectedX, float expectedY)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(0.01f));
        }
    }
}
