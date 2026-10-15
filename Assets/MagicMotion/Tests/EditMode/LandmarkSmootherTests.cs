using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class LandmarkSmootherTests
    {
        [Test]
        public void Process_SmoothsSameHandAcrossFrames()
        {
            var smoother = new LandmarkSmoother(0.5f);
            smoother.Process(HandTestFactory.Create(Handedness.Right, Vector3.zero));

            var result = smoother.Process(
                HandTestFactory.Create(Handedness.Right, new Vector3(2f, 0f, 0f)));

            Assert.That(result.Landmarks[0].Position.x, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Process_DoesNotMixLeftAndRightHistory()
        {
            var smoother = new LandmarkSmoother(0.5f);
            smoother.Process(HandTestFactory.Create(Handedness.Right, Vector3.zero));

            var left = smoother.Process(
                HandTestFactory.Create(Handedness.Left, new Vector3(4f, 0f, 0f)));

            Assert.That(left.Landmarks[0].Position.x, Is.EqualTo(4f).Within(0.0001f));
        }
    }
}
