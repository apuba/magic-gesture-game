using MagicMotion.Gesture;
using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class SwordFingerDetectorTests
    {
        [Test]
        public void IsSwordFinger_RecognizesTwoClosedAndTwoExtendedLongFingers()
        {
            var detector = new SwordFingerDetector();

            Assert.That(detector.IsSwordFinger(GestureHandFactory.SwordFinger()), Is.True);
        }

        [Test]
        public void IsSwordFinger_RejectsOpenPalm()
        {
            var detector = new SwordFingerDetector();

            Assert.That(detector.IsSwordFinger(GestureHandFactory.OpenPalm()), Is.False);
        }

        [Test]
        public void CalculateBrushPoint_ReturnsIndexAndMiddleTipMidpoint()
        {
            var hand = GestureHandFactory.SwordFinger();
            var expected = (HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip) +
                            HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleTip)) * 0.5f;

            Assert.That(SwordFingerDetector.CalculateBrushPoint(hand), Is.EqualTo(expected));
        }
    }
}
