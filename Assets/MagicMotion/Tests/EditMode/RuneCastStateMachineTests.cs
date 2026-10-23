using MagicMotion.Gesture;
using NUnit.Framework;

namespace MagicMotion.Tests.EditMode
{
    public sealed class RuneCastStateMachineTests
    {
        [Test]
        public void BackHeldIsTheSwordFingerStart()
        {
            var machine = new RuneCastStateMachine();

            machine.Update(FlipSequenceStage.BackHeld, false, false, 1d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.SwordReady));
        }

        [Test]
        public void TheCompletedTurnSummonsTheBaseRune()
        {
            var machine = new RuneCastStateMachine();
            machine.Update(FlipSequenceStage.BackHeld, false, false, 1d);
            machine.Update(FlipSequenceStage.SideSeen, false, false, 1.1d);

            machine.Update(FlipSequenceStage.Ready, false, false, 1.2d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.BaseRuneReady));
        }

        [Test]
        public void LockingAnElementArmsTheRune()
        {
            var machine = new RuneCastStateMachine();
            machine.Update(FlipSequenceStage.BackHeld, false, false, 1d);
            machine.Update(FlipSequenceStage.Ready, false, false, 1.1d);

            machine.Update(FlipSequenceStage.Ready, false, true, 1.2d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.RuneReady));
            Assert.That(machine.IsArmed, Is.True);
        }

        // Opening the hand is the way into the release, not the release itself.
        [Test]
        public void OpeningTheHandEntersTheReleaseStage()
        {
            var machine = new RuneCastStateMachine();
            machine.Update(FlipSequenceStage.Ready, false, true, 1d);

            machine.Update(FlipSequenceStage.Ready, true, true, 1.1d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Releasing));
            Assert.That(machine.IsArmed, Is.True);
        }

        [Test]
        public void ReturningToSwordFingerKeepsTheRuneArmed()
        {
            var machine = new RuneCastStateMachine();
            machine.Update(FlipSequenceStage.Ready, false, true, 1d, true);
            machine.Update(FlipSequenceStage.Ready, true, true, 1.1d, false);

            machine.Update(FlipSequenceStage.Ready, false, true, 1.2d, true);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.RuneReady));
            Assert.That(machine.IsArmed, Is.True);
        }

        [Test]
        public void LosingTheTurnCancelsTheCast()
        {
            var machine = new RuneCastStateMachine();
            machine.Update(FlipSequenceStage.Ready, false, true, 1d);

            machine.Update(FlipSequenceStage.Idle, false, true, 1.1d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Idle));
            Assert.That(machine.IsArmed, Is.False);
        }

        [Test]
        public void AnArmedRuneTimesOut()
        {
            var machine = new RuneCastStateMachine(0.5d, 1.5d, 0.6d);
            machine.Update(FlipSequenceStage.Ready, false, true, 1d);

            machine.Update(FlipSequenceStage.Ready, false, true, 1.4d);
            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.RuneReady));

            machine.Update(FlipSequenceStage.Ready, false, true, 1.6d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Idle));
        }

        [Test]
        public void AnUnreleasedRuneExpiresAfterTheReleaseWindow()
        {
            var machine = new RuneCastStateMachine(4d, 0.5d, 0.6d);
            machine.Update(FlipSequenceStage.Ready, false, true, 1d);
            machine.Update(FlipSequenceStage.Ready, true, true, 1.1d);
            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Releasing));

            machine.Update(FlipSequenceStage.Ready, true, true, 1.4d);
            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Releasing));

            machine.Update(FlipSequenceStage.Ready, true, true, 1.7d);

            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Idle));
        }

        [Test]
        public void ReleasingPutsTheMachineIntoCooldownAndBackToIdle()
        {
            var machine = new RuneCastStateMachine(4d, 1.5d, 0.3d);
            machine.Update(FlipSequenceStage.Ready, false, true, 1d);
            machine.Update(FlipSequenceStage.Ready, true, true, 1.1d);

            machine.Release(1.2d);
            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Cooldown));

            machine.Update(FlipSequenceStage.Ready, true, true, 1.6d);
            Assert.That(machine.Stage, Is.EqualTo(RuneCastStage.Idle));
        }
    }
}
