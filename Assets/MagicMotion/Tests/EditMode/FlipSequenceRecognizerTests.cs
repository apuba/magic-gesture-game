using MagicMotion.Gesture;
using NUnit.Framework;

namespace MagicMotion.Tests.EditMode
{
    public sealed class FlipSequenceRecognizerTests
    {
        [Test]
        public void BackThenSideThenPalmArmsTheRune()
        {
            var recognizer = new FlipSequenceRecognizer();

            Assert.That(recognizer.Update(true, HandFacing.BackToCamera, 1d), Is.False);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));

            Assert.That(recognizer.Update(true, HandFacing.BackToCamera, 1.21d), Is.False);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.BackHeld));

            Assert.That(recognizer.Update(true, HandFacing.SideToCamera, 1.5d), Is.False);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.SideSeen));

            // The palm end has to be held, not merely touched.
            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.6d), Is.False);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.SideSeen));

            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.73d), Is.True);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Ready));
        }

        [Test]
        public void PalmStraightFromBackStillArmsTheRuneOnceHeld()
        {
            var recognizer = new FlipSequenceRecognizer();
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);

            // A single palm frame must not arm a rune on its own.
            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.3d), Is.False);

            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.43d), Is.True);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Ready));
        }

        [Test]
        public void StrictModeRefusesAPalmThatSkipsTheSideStage()
        {
            var recognizer = new FlipSequenceRecognizer(requireSideStage: true);
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);

            recognizer.Update(true, HandFacing.PalmToCamera, 1.3d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.43d);

            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));
            Assert.That(recognizer.Ready, Is.False);
        }

        [Test]
        public void TurningPastTheWindowCancelsTheSequence()
        {
            var recognizer = new FlipSequenceRecognizer();
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);
            recognizer.Update(true, HandFacing.SideToCamera, 1.5d);

            // Window runs from the end of the back hold (1.21) for 2s.
            recognizer.Update(true, HandFacing.PalmToCamera, 3.5d);

            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));
        }

        [Test]
        public void StartingFromPalmNeverArmsTheRune()
        {
            var recognizer = new FlipSequenceRecognizer();

            recognizer.Update(true, HandFacing.PalmToCamera, 1d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.5d);

            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));
            Assert.That(recognizer.Ready, Is.False);
        }

        [Test]
        public void LosingTheFingerSignAfterArmingDropsTheRune()
        {
            var recognizer = new FlipSequenceRecognizer();
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);
            recognizer.Update(true, HandFacing.SideToCamera, 1.5d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.6d);
            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.73d), Is.True);

            // A quarter second of tracking noise must not drop an armed rune.
            Assert.That(recognizer.Update(false, HandFacing.Unknown, 1.8d), Is.True);
            Assert.That(recognizer.Update(false, HandFacing.Unknown, 2.04d), Is.True);

            Assert.That(recognizer.Update(false, HandFacing.Unknown, 2.07d), Is.False);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));
        }

        [Test]
        public void TurningBackMidFlipCarriesOnFromTheBackStage()
        {
            var recognizer = new FlipSequenceRecognizer();
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);
            recognizer.Update(true, HandFacing.SideToCamera, 1.4d);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.SideSeen));

            recognizer.Update(true, HandFacing.BackToCamera, 1.45d);
            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.BackHeld));

            // A second, complete turn still arms the rune.
            recognizer.Update(true, HandFacing.SideToCamera, 1.6d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.7d);
            Assert.That(recognizer.Update(true, HandFacing.PalmToCamera, 1.83d), Is.True);
        }

        [Test]
        public void ResetClearsAnArmedSequence()
        {
            var recognizer = new FlipSequenceRecognizer();
            recognizer.Update(true, HandFacing.BackToCamera, 1d);
            recognizer.Update(true, HandFacing.BackToCamera, 1.21d);
            recognizer.Update(true, HandFacing.SideToCamera, 1.5d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.6d);
            recognizer.Update(true, HandFacing.PalmToCamera, 1.73d);

            recognizer.Reset();

            Assert.That(recognizer.Stage, Is.EqualTo(FlipSequenceStage.Idle));
            Assert.That(recognizer.Ready, Is.False);
        }
    }
}
