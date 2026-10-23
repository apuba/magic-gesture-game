using System.Collections.Generic;
using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class HandGeometryTests
    {
        [Test]
        public void CalculatePalmWidth_IsTranslationIndependent()
        {
            var first = HandTestFactory.Create(Handedness.Right, Vector3.zero);
            var second = HandTestFactory.Create(Handedness.Right, new Vector3(20f, -5f, 3f));

            Assert.That(
                HandGeometry.CalculatePalmWidth(first),
                Is.EqualTo(HandGeometry.CalculatePalmWidth(second)).Within(0.0001f));
        }

        [Test]
        public void CalculatePalmNormal_FlipsForHandedness()
        {
            var left = HandTestFactory.Create(Handedness.Left, Vector3.zero);
            var right = HandTestFactory.Create(Handedness.Right, Vector3.zero);

            Assert.That(
                HandGeometry.CalculatePalmNormal(left),
                Is.EqualTo(-HandGeometry.CalculatePalmNormal(right)));
        }

        [TestCase(Handedness.Left, -0.1f, 0.1f)]
        [TestCase(Handedness.Right, 0.1f, -0.1f)]
        public void CalculatePalmNormal_PointsTowardCamera_WhenPalmFacesLens(
            Handedness handedness, float indexX, float pinkyX)
        {
            // Landmarks in the camera frame the runtime actually receives:
            // fingers above the wrist, index on the thumb side, palm facing
            // the lens (mirrors the on-device calibration data).
            var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
            for (var index = 0; index < HandData.RequiredLandmarkCount; index++)
            {
                landmarks.Add(new HandLandmark(index, Vector3.zero));
            }

            landmarks[(int)HandLandmarkIndex.Wrist] = new HandLandmark(0, new Vector3(0f, 0f, 0f));
            landmarks[(int)HandLandmarkIndex.IndexMcp] = new HandLandmark(5, new Vector3(indexX, 0.2f, 0f));
            landmarks[(int)HandLandmarkIndex.PinkyMcp] = new HandLandmark(17, new Vector3(pinkyX, 0.15f, 0f));
            var hand = new HandData(handedness, 0.95f, Vector3.zero, landmarks);

            var normal = HandGeometry.CalculatePalmNormal(hand);

            Assert.That(
                new PalmDirectionClassifier().Classify(normal),
                Is.EqualTo(PalmDirection.Forward));
        }

        [Test]
        public void PalmDirection_AcceptsForwardPalmWithStrongVerticalPitch()
        {
            // A hand held well above the camera has a strong vertical normal,
            // but is still facing the lens and must remain usable for casting.
            var direction = new PalmDirectionClassifier().Classify(
                new Vector3(0.08f, -0.92f, -0.38f),
                new Vector3(0.5f, 0.9f, 0f));

            Assert.That(direction, Is.EqualTo(PalmDirection.Forward));
        }

        [TestCase(0.08f, 0.50f, 0.44f, 0.02f, -0.90f)]
        [TestCase(0.92f, 0.50f, -0.44f, 0.02f, -0.90f)]
        [TestCase(0.50f, 0.92f, 0.02f, -0.44f, -0.90f)]
        [TestCase(0.50f, 0.08f, 0.02f, 0.44f, -0.90f)]
        public void PalmDirection_AcceptsPalmAimedAtLensFromImageEdges(
            float centerX,
            float centerY,
            float normalX,
            float normalY,
            float normalZ)
        {
            var direction = new PalmDirectionClassifier().Classify(
                new Vector3(normalX, normalY, normalZ),
                new Vector3(centerX, centerY, 0f));

            Assert.That(direction, Is.EqualTo(PalmDirection.Forward));
        }

        // The talisman is sized by the finger span, so a hand filling more of
        // the frame has to produce a proportionally larger measure.
        [Test]
        public void FingerSpan_ScalesWithTheHand()
        {
            var near = CreateFingerSpanHand(1f);
            var far = CreateFingerSpanHand(0.5f);

            var nearSpan = HandGeometry.CalculateFingerSpan(near, 16f / 9f);
            var farSpan = HandGeometry.CalculateFingerSpan(far, 16f / 9f);

            Assert.That(farSpan, Is.LessThan(nearSpan));
            Assert.That(farSpan, Is.EqualTo(nearSpan * 0.5f).Within(0.0001f));
        }

        // A hand pointing at the lens has most of its length along z. Ignoring
        // depth would measure it as almost nothing and shrink the rune away.
        [Test]
        public void FingerSpan_IncludesDepthWhenWeighted()
        {
            var tilted = CreateFingerSpanHand(1f, 0.4f);
            var flat = CreateFingerSpanHand(1f);

            var withDepth = HandGeometry.CalculateFingerSpan(tilted, 16f / 9f, 1f);
            var withoutDepth = HandGeometry.CalculateFingerSpan(tilted, 16f / 9f, 0f);
            var untilted = HandGeometry.CalculateFingerSpan(flat, 16f / 9f, 1f);

            Assert.That(withDepth, Is.GreaterThan(withoutDepth));
            Assert.That(withoutDepth, Is.EqualTo(untilted).Within(0.0001f));
        }

        [Test]
        public void FingerSpan_NeverCollapsesToZero()
        {
            var hand = CreateFingerSpanHand(0f);

            Assert.That(HandGeometry.CalculateFingerSpan(hand, 16f / 9f), Is.GreaterThan(0f));
        }

        // Wrist at the origin, fingers straight up, so scaling the landmarks
        // scales the hand about the wrist the way moving it towards the lens
        // does. Only the joints the span reads are given real positions.
        private static HandData CreateFingerSpanHand(float scale, float tipDepth = 0f)
        {
            var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
            for (var index = 0; index < HandData.RequiredLandmarkCount; index++)
            {
                landmarks.Add(new HandLandmark(index, Vector3.zero));
            }

            landmarks[(int)HandLandmarkIndex.Wrist] = new HandLandmark(0, new Vector3(0f, 0f, 0f));
            landmarks[(int)HandLandmarkIndex.IndexMcp] =
                new HandLandmark(5, new Vector3(-0.10f, 0.20f, 0f));
            landmarks[(int)HandLandmarkIndex.IndexTip] =
                new HandLandmark(8, new Vector3(-0.10f, 0.95f, tipDepth));
            landmarks[(int)HandLandmarkIndex.MiddleMcp] =
                new HandLandmark(9, new Vector3(0.02f, 0.22f, 0f));
            landmarks[(int)HandLandmarkIndex.MiddleTip] =
                new HandLandmark(12, new Vector3(0.02f, 1.05f, tipDepth));
            landmarks[(int)HandLandmarkIndex.RingMcp] =
                new HandLandmark(13, new Vector3(0.14f, 0.20f, 0f));
            landmarks[(int)HandLandmarkIndex.PinkyMcp] =
                new HandLandmark(17, new Vector3(0.25f, 0.16f, 0f));

            for (var index = 0; index < landmarks.Count; index++)
            {
                landmarks[index] =
                    new HandLandmark(index, landmarks[index].Position * scale);
            }

            return new HandData(Handedness.Right, 0.95f, Vector3.forward, landmarks);
        }

        [Test]
        public void PalmDirection_StillRejectsSidePalmWithSmallForwardComponent()
        {
            // Vertical pitch is tolerated; horizontal yaw is not, otherwise a
            // side-on hand could trigger a palm strike accidentally.
            var direction = new PalmDirectionClassifier().Classify(
                new Vector3(0.92f, -0.08f, -0.38f));

            Assert.That(direction, Is.Not.EqualTo(PalmDirection.Forward));
        }
    }
}

