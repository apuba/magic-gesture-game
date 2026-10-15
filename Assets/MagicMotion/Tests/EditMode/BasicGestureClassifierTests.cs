using MagicMotion.Gesture;
using NUnit.Framework;

namespace MagicMotion.Tests
{
    public sealed class BasicGestureClassifierTests
    {
        [Test]
        public void Classify_RecognizesOpenPalm()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.OpenPalm());

            Assert.That(result.Type, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(result.Fingers.ExtendedCount, Is.EqualTo(5));
        }

        [Test]
        public void Classify_RecognizesFist()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.Fist());

            Assert.That(result.Type, Is.EqualTo(GestureType.Fist));
            Assert.That(result.Fingers.ExtendedCount, Is.Zero);
        }

        [Test]
        public void Classify_RecognizesOpenPalmWithBentThumb()
        {
            var classifier = new BasicGestureClassifier();

            var result = classifier.Classify(GestureHandFactory.OpenPalmWithBentThumb());

            Assert.That(result.Type, Is.EqualTo(GestureType.OpenPalm));
            Assert.That(result.Fingers.Thumb, Is.EqualTo(FingerState.Bent));
        }
    }
}

