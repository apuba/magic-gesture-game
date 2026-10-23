using System;
using UnityEngine;

namespace MagicMotion.Gesture
{
    /// <summary>
    /// Picks the element from the direction the casting hand travels once the
    /// base rune is on screen: up thunder, down fire, left water, right wind.
    /// </summary>
    /// <remarks>
    /// The measurement is a displacement from the point the rune was summoned
    /// at, not an absolute position, so where in the frame the player happens
    /// to cast from does not matter.
    ///
    /// Two gates have to agree before an element is committed. The travel has
    /// to clear a minimum distance, otherwise a hand that is merely trembling
    /// picks an element. And the dominant axis has to win by a margin,
    /// otherwise a diagonal drift, which is where most hand movements land,
    /// would pick one element on one frame and the other on the next. The
    /// winner then has to hold for a short time rather than a single frame,
    /// for the same reason.
    ///
    /// The direction is judged in screen coordinates, so up and down mean up
    /// and down to the player regardless of how the phone is rotated or
    /// whether the camera image is mirrored. The values below are the
    /// starting suggestions from the design document and still have to be
    /// calibrated on device.
    /// </remarks>
    public sealed class DirectionSelectionRecognizer
    {
        private readonly double deadlineSeconds;
        private readonly float minimumTravel;
        private readonly float minimumDominance;
        private readonly double confirmationSeconds;

        public DirectionSelectionRecognizer(
            double deadlineSeconds = 1.2d,
            float minimumTravel = 0.15f,
            float minimumDominance = 1.25f,
            double confirmationSeconds = 0.12d)
        {
            this.deadlineSeconds = Math.Max(0d, deadlineSeconds);
            this.minimumTravel = Mathf.Max(0.01f, minimumTravel);
            this.minimumDominance = Mathf.Max(1f, minimumDominance);
            this.confirmationSeconds = Math.Max(0d, confirmationSeconds);
        }

        public RuneElement Element { get; private set; } = RuneElement.None;

        /// <summary>
        /// The direction currently being previewed after it clears the travel
        /// threshold, before the short confirmation window locks Element.
        /// </summary>
        public RuneElement Candidate => candidate;

        public Vector2 Travel =>
            double.IsNegativeInfinity(startedAt) ? Vector2.zero : currentPosition - anchor;

        public bool Locked => Element != RuneElement.None;

        /// <summary>
        /// True once the deadline passed without a direction, which cancels the
        /// whole cast.
        /// </summary>
        public bool Expired { get; private set; }

        private Vector2 anchor;
        private Vector2 currentPosition;
        private double startedAt = double.NegativeInfinity;
        private RuneElement candidate = RuneElement.None;
        private double candidateSince = double.NegativeInfinity;

        public void Begin(Vector2 anchorPosition, double timestamp)
        {
            anchor = anchorPosition;
            currentPosition = anchorPosition;
            startedAt = timestamp;
            Element = RuneElement.None;
            Expired = false;
            candidate = RuneElement.None;
            candidateSince = double.NegativeInfinity;
        }

        public void Reset()
        {
            Element = RuneElement.None;
            Expired = false;
            startedAt = double.NegativeInfinity;
            candidate = RuneElement.None;
            candidateSince = double.NegativeInfinity;
        }

        /// <summary>
        /// Feeds the current hand position. Both positions are in units of the
        /// frame's shorter side, with y increasing downwards, so an upward move
        /// of the hand is -y.
        /// </summary>
        public RuneElement Update(Vector2 position, double timestamp)
        {
            if (Locked || Expired)
            {
                return Element;
            }

            if (double.IsNegativeInfinity(startedAt))
            {
                return RuneElement.None;
            }

            currentPosition = position;

            if (timestamp - startedAt > deadlineSeconds)
            {
                Expired = true;
                return RuneElement.None;
            }

            var travel = position - anchor;
            var absoluteX = Mathf.Abs(travel.x);
            var absoluteY = Mathf.Abs(travel.y);

            // An axis only wins if it clearly beats the other one. A movement
            // that is halfway between two directions belongs to neither, and
            // picking one anyway would let the element flip on noise.
            RuneElement next;
            if (absoluteY >= absoluteX * minimumDominance)
            {
                next = absoluteY < minimumTravel
                    ? RuneElement.None
                    : (travel.y < 0f ? RuneElement.Thunder : RuneElement.Fire);
            }
            else if (absoluteX >= absoluteY * minimumDominance)
            {
                next = absoluteX < minimumTravel
                    ? RuneElement.None
                    : (travel.x < 0f ? RuneElement.Water : RuneElement.Wind);
            }
            else
            {
                next = RuneElement.None;
            }

            if (next != candidate)
            {
                candidate = next;
                candidateSince = timestamp;
                return RuneElement.None;
            }

            if (candidate != RuneElement.None &&
                timestamp - candidateSince >= confirmationSeconds)
            {
                Element = candidate;
            }

            return Element;
        }
    }
}
