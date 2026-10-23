using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.UI
{
    /// <summary>
    /// Small, presentation-only state machine for the first rune classroom loop.
    /// It deliberately owns no gesture, skill, or combat state: a caller gives
    /// it the current sword-pose verdict and it only decides whether the visual
    /// should be shown and where its unsmoothed target is.
    /// </summary>
    public sealed class RuneFollowTracker
    {
        private readonly double acquireSeconds;
        private readonly double lossGraceSeconds;
        private double candidateSince = double.NegativeInfinity;
        private double lastSeenAt = double.NegativeInfinity;

        public RuneFollowTracker(double acquireSeconds = 0.2d, double lossGraceSeconds = 0.18d)
        {
            this.acquireSeconds = System.Math.Max(0d, acquireSeconds);
            this.lossGraceSeconds = System.Math.Max(0d, lossGraceSeconds);
        }

        public bool Visible { get; private set; }
        public Vector3 Target { get; private set; }

        public void UpdatePose(bool swordFinger, Vector3 target, double timestamp)
        {
            if (swordFinger)
            {
                Target = target;
                lastSeenAt = timestamp;
                if (double.IsNegativeInfinity(candidateSince))
                {
                    candidateSince = timestamp;
                }

                if (timestamp - candidateSince >= acquireSeconds)
                {
                    Visible = true;
                }

                return;
            }

            candidateSince = double.NegativeInfinity;
            if (Visible && timestamp - lastSeenAt <= lossGraceSeconds)
            {
                return;
            }

            Visible = false;
        }

        public void Reset()
        {
            Visible = false;
            candidateSince = double.NegativeInfinity;
            lastSeenAt = double.NegativeInfinity;
        }

        /// <summary>
        /// Places the rune beyond the joined fingertips, rather than directly
        /// over them. The offset is relative to the hand itself so it behaves
        /// consistently at different camera distances.
        /// </summary>
        public static Vector3 CalculateTarget(HandData hand, float forwardPalmWidths = 0.32f)
        {
            var indexTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip);
            var middleTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleTip);
            var indexMcp = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexMcp);
            var middleMcp = HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleMcp);
            var tips = (indexTip + middleTip) * 0.5f;
            var roots = (indexMcp + middleMcp) * 0.5f;
            var direction = tips - roots;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector3.up;
            }

            return tips + direction.normalized * HandGeometry.CalculatePalmWidth(hand) *
                Mathf.Max(0f, forwardPalmWidths);
        }

        /// <summary>
        /// Where the rune sits while the player is opening the hand to release
        /// it: in front of the palm rather than beyond the fingertips, because
        /// the fingers are no longer joined to point from. Same reasoning about
        /// the offset being relative to the hand, so the distance does not
        /// change with how far the hand is from the lens.
        /// </summary>
        public static Vector3 CalculatePalmAnchor(
            HandData hand, float widthToHeight, float forwardPalmWidths = 0.35f)
        {
            var center = HandGeometry.CalculatePalmCenter(hand);
            var normal = hand.PalmNormal;
            if (normal.sqrMagnitude <= 0.000001f)
            {
                return center;
            }

            var aspect = Mathf.Max(widthToHeight, 0.0001f);

            // The normal is a direction in normalized coordinates, so it has
            // to be carried into square units before a palm width can scale it,
            // then carried back out to sit alongside the landmarks.
            var squared = HandGeometry.SquareDirection(normal.normalized, aspect);
            if (squared.sqrMagnitude <= 0.000001f)
            {
                return center;
            }

            var distance = HandGeometry.CalculateSquarePalmWidth(hand, aspect) *
                Mathf.Max(0f, forwardPalmWidths);
            var offset = squared.normalized * distance;
            return center + new Vector3(offset.x / aspect, offset.y, offset.z);
        }
    }
}
