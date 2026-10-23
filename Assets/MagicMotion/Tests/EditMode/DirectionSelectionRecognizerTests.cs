using MagicMotion.Gesture;
using NUnit.Framework;
using UnityEngine;

namespace MagicMotion.Tests.EditMode
{
    public sealed class DirectionSelectionRecognizerTests
    {
        private static readonly Vector2 Origin = new Vector2(0.5f, 0.5f);

        [Test]
        public void BeginStartsWithZeroTravelAtTheCapturedAnchor()
        {
            var recognizer = new DirectionSelectionRecognizer();

            recognizer.Begin(Origin, 1d);

            Assert.That(recognizer.Travel, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void UpPicksThunder()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0f, -0.2f), 1.05d);
            var result = recognizer.Update(Origin + new Vector2(0f, -0.2f), 1.2d);

            Assert.That(result, Is.EqualTo(RuneElement.Thunder));
            Assert.That(recognizer.Locked, Is.True);
        }

        [Test]
        public void DownPicksFire()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0f, 0.2f), 1.05d);

            Assert.That(recognizer.Update(Origin + new Vector2(0f, 0.2f), 1.2d),
                Is.EqualTo(RuneElement.Fire));
        }

        [Test]
        public void LeftPicksWater()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(-0.2f, 0f), 1.05d);

            Assert.That(recognizer.Update(Origin + new Vector2(-0.2f, 0f), 1.2d),
                Is.EqualTo(RuneElement.Water));
        }

        [Test]
        public void RightPicksWind()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0.2f, 0f), 1.05d);

            Assert.That(recognizer.Update(Origin + new Vector2(0.2f, 0f), 1.2d),
                Is.EqualTo(RuneElement.Wind));
        }

        // A hand held still never picks an element, however long it waits.
        [Test]
        public void MovementUnderTheThresholdDoesNotPick()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0.05f, 0f), 1.05d);
            var result = recognizer.Update(Origin + new Vector2(0.05f, 0f), 1.3d);

            Assert.That(result, Is.EqualTo(RuneElement.None));
            Assert.That(recognizer.Expired, Is.False);
        }

        // A perfect diagonal belongs to neither axis, so it must not decide.
        [Test]
        public void ADiagonalWithNoDominantAxisDoesNotPick()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0.2f, -0.2f), 1.05d);
            var result = recognizer.Update(Origin + new Vector2(0.2f, -0.2f), 1.3d);

            Assert.That(result, Is.EqualTo(RuneElement.None));
        }

        [Test]
        public void MissingTheDeadlineExpiresTheChoice()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin, 1.5d);
            var result = recognizer.Update(Origin, 2.3d);

            Assert.That(result, Is.EqualTo(RuneElement.None));
            Assert.That(recognizer.Expired, Is.True);
        }

        // The element is locked once picked; moving afterwards must not
        // re-pick, or a spell would change while it is being released.
        [Test]
        public void ALockedElementDoesNotChange()
        {
            var recognizer = new DirectionSelectionRecognizer();
            recognizer.Begin(Origin, 1d);

            recognizer.Update(Origin + new Vector2(0f, -0.2f), 1.05d);
            Assert.That(recognizer.Update(Origin + new Vector2(0f, -0.2f), 1.2d),
                Is.EqualTo(RuneElement.Thunder));

            var later = recognizer.Update(Origin + new Vector2(0f, 0.3f), 1.9d);

            Assert.That(later, Is.EqualTo(RuneElement.Thunder));
        }
    }
}
