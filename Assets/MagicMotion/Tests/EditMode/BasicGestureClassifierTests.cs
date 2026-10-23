using System.Collections.Generic;
using MagicMotion.Gesture;
using MagicMotion.Vision;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests
{
    public sealed class BasicGestureClassifierTests
    {
        // The factory hands are laid out in a square frame, so the frame shape
        // that keeps every angle as authored is 1.
        private const float SquareFrame = 1f;

        [Test]
        public void Classify_RecognizesOpenPalm()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.OpenPalm(), SquareFrame);

            Assert.That(result.Type, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(result.Fingers.ExtendedCount, Is.EqualTo(5));
        }

        [Test]
        public void Classify_RecognizesFist()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.Fist(), SquareFrame);

            Assert.That(result.Type, Is.EqualTo(GestureType.Fist));
            Assert.That(result.Fingers.ExtendedCount, Is.Zero);
        }

        [Test]
        public void Classify_RecognizesOpenPalmWithBentThumb()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.OpenPalmWithBentThumb(), SquareFrame);

            Assert.That(result.Type, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(result.Fingers.Thumb, Is.EqualTo(FingerState.Bent));
        }

        // A joint angle is the angle between two offsets, and normalized offsets
        // are squashed differently along x and y, so on a 16:9 frame this bent
        // thumb measured 135 degrees with the hand lying one way and 150 the
        // other, straddling the extended threshold and flipping the state of a
        // finger every time the phone turned. Squashing the frame and telling
        // the classifier its shape has to give back the very same fingers.
        [Test]
        public void Classify_ReadsTheSameFingersOnASquashedFrame()
        {
            var classifier = new BasicGestureClassifier();
            const float widthToHeight = 16f / 9f;
            var hand = Squash(GestureHandFactory.OpenPalmWithBentThumb(), widthToHeight);

            var result = classifier.Classify(hand, widthToHeight);

            Assert.That(result.Type, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(result.Fingers.Thumb, Is.EqualTo(FingerState.Bent));
        }

        private static HandData Squash(HandData hand, float widthToHeight)
        {
            var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
            for (var index = 0; index < hand.Landmarks.Count; index++)
            {
                var landmark = hand.Landmarks[index];
                landmarks.Add(new HandLandmark(landmark.Index, new Vector3(
                    landmark.Position.x / widthToHeight,
                    landmark.Position.y,
                    landmark.Position.z / widthToHeight)));
            }

            return new HandData(hand.Handedness, hand.Confidence, hand.PalmNormal, landmarks);
        }
    }
}

