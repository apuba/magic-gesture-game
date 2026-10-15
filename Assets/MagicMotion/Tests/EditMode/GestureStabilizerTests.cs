using MagicMotion.Gesture;
using MagicMotion.Vision;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class GestureStabilizerTests
    {
        [Test]
        public void TryConfirm_RequiresConsecutiveFramesAndEmitsOnce()
        {
            var stabilizer = new GestureStabilizer(3);

            Assert.That(stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _), Is.False);
            Assert.That(stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _), Is.False);
            Assert.That(stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out var confirmed), Is.True);
            Assert.That(confirmed, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _), Is.False);
        }

        [Test]
        public void TryConfirm_TracksHandsIndependently()
        {
            var stabilizer = new GestureStabilizer(2);
            stabilizer.TryConfirm(Handedness.Left, GestureType.Fist, out _);
            stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _);

            Assert.That(stabilizer.TryConfirm(Handedness.Left, GestureType.Fist, out var left), Is.True);
            Assert.That(left, Is.EqualTo(GestureType.Fist));
            Assert.That(stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out var right), Is.True);
            Assert.That(right, Is.EqualTo(GestureType.OpenPalm));
        }

        [Test]
        public void TryReset_ReturnsPreviouslyEmittedGestureOnce()
        {
            var stabilizer = new GestureStabilizer(1);
            stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _);

            Assert.That(stabilizer.TryReset(Handedness.Right, out var lost), Is.True);
            Assert.That(lost, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(stabilizer.TryReset(Handedness.Right, out _), Is.False);
        }

        [Test]
        public void TryResetAfterMissing_ToleratesShortTrackingDropout()
        {
            var stabilizer = new GestureStabilizer(1, 3);
            stabilizer.TryConfirm(Handedness.Right, GestureType.OpenPalm, out _);

            Assert.That(stabilizer.TryResetAfterMissing(Handedness.Right, out _), Is.False);
            Assert.That(stabilizer.TryResetAfterMissing(Handedness.Right, out _), Is.False);
            Assert.That(stabilizer.TryResetAfterMissing(Handedness.Right, out var lost), Is.True);
            Assert.That(lost, Is.EqualTo(GestureType.OpenPalm));
        }
    }
}
