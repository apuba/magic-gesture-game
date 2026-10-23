using System;

namespace MagicMotion.Gesture
{
    /// <summary>
    /// Tracks one casting attempt across its stages. It is driven once per
    /// frame with the verdicts the detectors already produced, and answers
    /// which stage the attempt is in.
    /// </summary>
    /// <remarks>
    /// The distinction that matters is between the direction-picking stage and
    /// the armed stage, because they behave differently when the finger sign
    /// drops. While the element is still being picked, dropping the sign means
    /// the player stopped mid-gesture, so the attempt is cancelled. Once the
    /// element is locked the player is expected to open the hand to prepare
    /// the release, so dropping the sign there is a transition and not a
    /// mistake; only running out of time, or pushing, ends it.
    /// </remarks>
    public sealed class RuneCastStateMachine
    {
        private readonly double armedTimeoutSeconds;
        private readonly double releaseWindowSeconds;
        private readonly double cooldownSeconds;

        public RuneCastStateMachine(
            double armedTimeoutSeconds = 4d,
            double releaseWindowSeconds = 10d,
            double cooldownSeconds = 0.6d)
        {
            this.armedTimeoutSeconds = Math.Max(0d, armedTimeoutSeconds);
            this.releaseWindowSeconds = Math.Max(0d, releaseWindowSeconds);
            this.cooldownSeconds = Math.Max(0d, cooldownSeconds);
        }

        public RuneCastStage Stage { get; private set; } = RuneCastStage.Idle;

        /// <summary>
        /// True while a rune is on screen waiting to be released.
        /// </summary>
        public bool IsArmed =>
            Stage == RuneCastStage.RuneReady || Stage == RuneCastStage.Releasing;

        private double stageSince = double.NegativeInfinity;

        public void Update(
            FlipSequenceStage summon,
            bool openPalm,
            bool elementLocked,
            double timestamp,
            bool swordFinger = false)
        {
            switch (summon)
            {
                case FlipSequenceStage.BackHeld:
                    Enter(RuneCastStage.SwordReady, timestamp);
                    break;

                case FlipSequenceStage.SideSeen:
                    Enter(RuneCastStage.Flipping, timestamp);
                    break;

                case FlipSequenceStage.Ready:
                    UpdateSummoned(openPalm, swordFinger, elementLocked, timestamp);
                    break;

                default:
                    Enter(RuneCastStage.Idle, timestamp);
                    break;
            }

            if (Stage == RuneCastStage.RuneReady &&
                timestamp - stageSince > armedTimeoutSeconds)
            {
                Reset();
            }

            if (Stage == RuneCastStage.Releasing &&
                timestamp - stageSince > releaseWindowSeconds)
            {
                Reset();
            }

            if (Stage == RuneCastStage.Cooldown &&
                timestamp - stageSince >= cooldownSeconds)
            {
                Reset();
            }
        }

        /// <summary>
        /// Ends the attempt after the spell has been released and puts the
        /// machine into cooldown.
        /// </summary>
        public void Release(double timestamp)
        {
            Enter(RuneCastStage.Cooldown, timestamp);
        }

        public void Reset()
        {
            Stage = RuneCastStage.Idle;
            stageSince = double.NegativeInfinity;
        }

        private void UpdateSummoned(
            bool openPalm, bool swordFinger, bool elementLocked, double timestamp)
        {
            if (Stage == RuneCastStage.Idle ||
                Stage == RuneCastStage.SwordReady ||
                Stage == RuneCastStage.Flipping)
            {
                Enter(RuneCastStage.BaseRuneReady, timestamp);
            }

            if (Stage == RuneCastStage.BaseRuneReady && elementLocked)
            {
                Enter(RuneCastStage.RuneReady, timestamp);
            }

            // Opening the hand is the way into the release, never the release
            // itself; only a real push launches the spell.
            if (Stage == RuneCastStage.RuneReady && openPalm)
            {
                Enter(RuneCastStage.Releasing, timestamp);
            }
            else if (Stage == RuneCastStage.Releasing && swordFinger)
            {
                // The selected rune can be held with either supported pose.
                // Returning to the sword-finger pose moves it from the palm
                // back to the fingertips without cancelling the cast.
                Enter(RuneCastStage.RuneReady, timestamp);
            }
        }

        private void Enter(RuneCastStage stage, double timestamp)
        {
            if (Stage == stage)
            {
                return;
            }

            Stage = stage;
            stageSince = timestamp;
        }
    }
}
