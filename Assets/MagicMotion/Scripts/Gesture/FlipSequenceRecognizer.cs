using System;

namespace MagicMotion.Gesture
{
    public enum FlipSequenceStage
    {
        Idle,
        BackHeld,
        SideSeen,
        Ready
    }

    /// <summary>
    /// Decides whether the player has performed the summoning turn, and only
    /// then arms the base rune. It owns no drawing and no skill state: a caller
    /// feeds it the current pose verdict and it answers "armed" or "not".
    /// </summary>
    /// <remarks>
    /// The turn is what separates a deliberate cast from simply holding the
    /// finger sign where it happens to be read as palm-first for a frame, so
    /// the order is the rule, not the destination. Back, then side, then palm;
    /// a result that jumps straight to palm is a misread, not a summon, and is
    /// thrown away.
    ///
    /// All timing is wall clock, in the same units the vision events carry, so
    /// the tolerances mean the same thing whatever rate the tracker happens to
    /// run at. The values below are the starting suggestions from the design
    /// document and still have to be calibrated on device.
    /// </remarks>
    public sealed class FlipSequenceRecognizer
    {
        private readonly double startHoldSeconds;
        private readonly double flipWindowSeconds;
        private readonly double endHoldSeconds;
        private readonly double lostGraceSeconds;
        private readonly bool requireSideStage;

        /// <summary>
        /// The defaults are the relaxed end of the range the design document
        /// suggests: they were calibrated for "is the player trying to cast",
        /// not for "is the pose textbook". Tighten them from the device
        /// calibration screen once mis-trigger rates are measured.
        /// </summary>
        /// <param name="startHoldSeconds">How long the back-facing sword
        /// finger has to hold before the turn is allowed to start.</param>
        /// <param name="flipWindowSeconds">How long the whole turn may take
        /// from that moment.</param>
        /// <param name="endHoldSeconds">How long the palm end has to be held
        /// before the rune is armed.</param>
        /// <param name="lostGraceSeconds">How long the finger sign may drop
        /// before the rune is cancelled.</param>
        /// <param name="requireSideStage">When true, a reading that jumps
        /// straight from back to palm is thrown away as a misread. Off by
        /// default: at tracking rate the side of the hand can pass between two
        /// frames, and refusing those casts felt like the game not working.
        /// The end hold still has to be held, so a single stray palm frame
        /// cannot arm a rune on its own.</param>
        public FlipSequenceRecognizer(
            double startHoldSeconds = 0.2d,
            double flipWindowSeconds = 2d,
            double endHoldSeconds = 0.12d,
            double lostGraceSeconds = 0.25d,
            bool requireSideStage = false)
        {
            this.startHoldSeconds = Math.Max(0d, startHoldSeconds);
            this.flipWindowSeconds = Math.Max(0d, flipWindowSeconds);
            this.endHoldSeconds = Math.Max(0d, endHoldSeconds);
            this.lostGraceSeconds = Math.Max(0d, lostGraceSeconds);
            this.requireSideStage = requireSideStage;
        }

        public FlipSequenceStage Stage { get; private set; } = FlipSequenceStage.Idle;

        // Armed means the turn is complete and a rune may be shown. It stays
        // armed for as long as the finger sign is held, and is dropped the
        // moment the hand stops making it.
        public bool Ready => Stage == FlipSequenceStage.Ready;

        public bool LastSwordFinger { get; private set; }
        public HandFacing LastFacing { get; private set; } = HandFacing.Unknown;

        private double stageSince = double.NegativeInfinity;
        private double flipStartAt = double.NegativeInfinity;
        private double poseLostSince = double.NegativeInfinity;
        private bool holding;

        public bool Update(bool swordFinger, HandFacing facing, double timestamp)
        {
            LastSwordFinger = swordFinger;
            LastFacing = facing;

            // While an armed rune is waiting to be released the player is
            // expected to leave the finger sign and open the hand, so the pose
            // check must stop cancelling the sequence; the caller holds it open
            // for that stage.
            if (holding)
            {
                if (Stage == FlipSequenceStage.Idle)
                {
                    Stage = FlipSequenceStage.Ready;
                }

                return Ready;
            }

            // The sign has to be held right through the turn. A frame or two
            // that reads as something else is tracking noise; longer than that
            // is the player's hand doing something else.
            if (!swordFinger)
            {
                if (!double.IsNegativeInfinity(poseLostSince) &&
                    timestamp - poseLostSince > lostGraceSeconds)
                {
                    Reset();
                    return false;
                }

                if (double.IsNegativeInfinity(poseLostSince))
                {
                    poseLostSince = timestamp;
                }

                return Ready;
            }

            poseLostSince = double.NegativeInfinity;

            switch (Stage)
            {
                case FlipSequenceStage.Idle:
                    UpdateIdle(facing, timestamp);
                    break;

                case FlipSequenceStage.BackHeld:
                    UpdateBackHeld(facing, timestamp);
                    break;

                case FlipSequenceStage.SideSeen:
                    UpdateSideSeen(facing, timestamp);
                    break;

                case FlipSequenceStage.Ready:
                    // Once armed only the pose matters; turning the hand away
                    // again is handled further up.
                    break;
            }

            return Ready;
        }

        public void Reset()
        {
            Stage = FlipSequenceStage.Idle;
            stageSince = double.NegativeInfinity;
            flipStartAt = double.NegativeInfinity;
            poseLostSince = double.NegativeInfinity;
            holding = false;
        }

        /// <summary>
        /// Keeps the sequence armed across the sword-finger to open-palm
        /// transition. Without it the hand would have to hold a pose it is in
        /// the middle of leaving, which cancels the cast before the push that
        /// is supposed to release it can arrive.
        /// </summary>
        public void Hold(bool value)
        {
            holding = value;
        }

        private void UpdateIdle(HandFacing facing, double timestamp)
        {
            if (facing != HandFacing.BackToCamera)
            {
                stageSince = double.NegativeInfinity;
                return;
            }

            if (double.IsNegativeInfinity(stageSince))
            {
                stageSince = timestamp;
                return;
            }

            if (timestamp - stageSince >= startHoldSeconds)
            {
                Stage = FlipSequenceStage.BackHeld;
                flipStartAt = timestamp;
                stageSince = double.NegativeInfinity;
            }
        }

        private void UpdateBackHeld(HandFacing facing, double timestamp)
        {
            if (timestamp - flipStartAt > flipWindowSeconds)
            {
                Reset();
                return;
            }

            if (facing == HandFacing.SideToCamera)
            {
                Stage = FlipSequenceStage.SideSeen;
                return;
            }

            if (facing == HandFacing.PalmToCamera)
            {
                // The side of the hand is only a step in the turn, not a
                // requirement: at tracking rate it can pass between two
                // frames, and rejecting those casts reads as the game not
                // working. The palm still has to be held, so a single stray
                // frame cannot arm a rune on its own.
                if (requireSideStage)
                {
                    Reset();
                    return;
                }

                Stage = FlipSequenceStage.SideSeen;
                UpdatePalm(timestamp);
            }
        }

        private void UpdateSideSeen(HandFacing facing, double timestamp)
        {
            if (timestamp - flipStartAt > flipWindowSeconds)
            {
                Reset();
                return;
            }

            if (facing == HandFacing.PalmToCamera)
            {
                UpdatePalm(timestamp);
                return;
            }

            // Turning back is a continuation of the same turn, not a failure:
            // start the palm hold over rather than dropping the sequence.
            stageSince = double.NegativeInfinity;

            if (facing == HandFacing.BackToCamera)
            {
                Stage = FlipSequenceStage.BackHeld;
            }
        }

        private void UpdatePalm(double timestamp)
        {
            if (double.IsNegativeInfinity(stageSince))
            {
                stageSince = timestamp;
                return;
            }

            if (timestamp - stageSince >= endHoldSeconds)
            {
                Stage = FlipSequenceStage.Ready;
                stageSince = double.NegativeInfinity;
            }
        }
    }
}
