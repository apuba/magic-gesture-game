using MagicMotion.Gesture;
using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class SwordFingerDetectorTests
    {
        // The factory hands are laid out in a square frame, so the frame shape
        // that keeps every angle as authored is 1.
        private const float SquareFrame = 1f;

        [Test]
        public void IsSwordFinger_RecognizesTwoClosedAndTwoExtendedLongFingers()
        {
            var detector = new SwordFingerDetector();

            Assert.That(detector.IsSwordFinger(GestureHandFactory.SwordFinger(), SquareFrame), Is.True);
        }

        [Test]
        public void IsSwordFinger_RejectsOpenPalm()
        {
            var detector = new SwordFingerDetector();

            Assert.That(detector.IsSwordFinger(GestureHandFactory.OpenPalm(), SquareFrame), Is.False);
        }

        // Turning the back of the hand to the camera hides the thumb, and the
        // tracker then reports it as Extended on some frames and Bent on
        // others. It must not be what decides whether the hand is pointing.
        [Test]
        public void IsPointing_IgnoresTheThumb()
        {
            var detector = new SwordFingerDetector();
            var hand = GestureHandFactory.PointingIndexWithThumbOut();

            Assert.That(detector.Analyze(hand, SquareFrame).Thumb, Is.EqualTo(FingerState.Extended));
            Assert.That(detector.IsPointing(hand, SquareFrame), Is.True);
        }

        [Test]
        public void IsRuneFinger_IgnoresAnExtendedThumbButKeepsTwoFingerPose()
        {
            var detector = new SwordFingerDetector();
            var hand = GestureHandFactory.PointingIndexWithThumbOut();

            Assert.That(detector.IsSwordFinger(hand, SquareFrame), Is.False);
            Assert.That(detector.IsRuneFinger(hand, SquareFrame), Is.True);
        }

        [Test]
        public void IsPointing_AcceptsTheVisibleRuneFeedbackPose()
        {
            var detector = new SwordFingerDetector();
            var hand = GestureHandFactory.PointingIndexWithThumbOut();

            Assert.That(detector.IsPointing(hand, SquareFrame), Is.True);
        }

        [Test]
        public void CalculateBrushPoint_ReturnsIndexTip()
        {
            var hand = GestureHandFactory.SwordFinger();
            var expected = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip);

            Assert.That(SwordFingerDetector.CalculateBrushPoint(hand), Is.EqualTo(expected));
        }
    }
}
