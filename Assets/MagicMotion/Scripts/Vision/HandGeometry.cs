using System;
using UnityEngine;

namespace MagicMotion.Vision
{
    public static class HandGeometry
    {
        private const float MinimumScale = 0.0001f;

        public static Vector3 GetPosition(HandData hand, HandLandmarkIndex index)
        {
            if (hand == null || !hand.IsValid)
            {
                throw new ArgumentException("A valid 21-landmark hand is required.", nameof(hand));
            }

            return hand.Landmarks[(int)index].Position;
        }

        public static float CalculatePalmWidth(HandData hand)
        {
            var indexMcp = GetPosition(hand, HandLandmarkIndex.IndexMcp);
            var pinkyMcp = GetPosition(hand, HandLandmarkIndex.PinkyMcp);
            return Mathf.Max(Vector3.Distance(indexMcp, pinkyMcp), MinimumScale);
        }

        /// <summary>
        /// Distance in units that are square on the frame. Normalized x spans
        /// width pixels and y spans height pixels, so on a 16:9 frame a unit of
        /// x covers almost twice as much of the hand as a unit of y: distances
        /// and angles taken straight from normalized coordinates come out
        /// different for the same hand lying one way or the other in the frame,
        /// which is what made joint angles straddle their threshold as the
        /// phone turned. Stretching x by width / height puts both axes on one
        /// scale.
        /// </summary>
        public static float SquareDistance(
            HandData hand,
            HandLandmarkIndex first,
            HandLandmarkIndex second,
            float widthToHeight)
        {
            return SquareDirection(
                GetPosition(hand, second) - GetPosition(hand, first), widthToHeight).magnitude;
        }

        /// <summary>
        /// Stretches a normalized offset onto a square frame, so an angle
        /// between two of them means the same thing whichever way the hand
        /// happens to lie.
        /// </summary>
        public static Vector3 SquareDirection(Vector3 offset, float widthToHeight)
        {
            return new Vector3(offset.x * widthToHeight, offset.y, offset.z * widthToHeight);
        }

        public static float CalculateSquarePalmWidth(HandData hand, float widthToHeight)
        {
            return Mathf.Max(
                SquareDistance(
                    hand, HandLandmarkIndex.IndexMcp, HandLandmarkIndex.PinkyMcp, widthToHeight),
                MinimumScale);
        }

        public static Vector3 CalculatePalmCenter(HandData hand)
        {
            return (
                GetPosition(hand, HandLandmarkIndex.Wrist) +
                GetPosition(hand, HandLandmarkIndex.IndexMcp) +
                GetPosition(hand, HandLandmarkIndex.MiddleMcp) +
                GetPosition(hand, HandLandmarkIndex.RingMcp) +
                GetPosition(hand, HandLandmarkIndex.PinkyMcp)) / 5f;
        }

        public static Vector3 CalculatePalmNormal(HandData hand)
        {
            var wrist = GetPosition(hand, HandLandmarkIndex.Wrist);
            var index = GetPosition(hand, HandLandmarkIndex.IndexMcp);
            var pinky = GetPosition(hand, HandLandmarkIndex.PinkyMcp);
            var normal = Vector3.Cross(index - wrist, pinky - wrist);

            if (normal.sqrMagnitude < MinimumScale * MinimumScale)
            {
                return Vector3.zero;
            }

            normal.Normalize();
            // The raw cross product points out of the back of the hand (verified
            // on device: palms facing the lens produce a positive z). Negate it
            // so the result is the palm-facing direction: z < 0 when the palm
            // faces the camera.
            return hand.Handedness == Handedness.Left ? normal : -normal;
        }

        /// <summary>
        /// Distance from the joined fingertips to the middle of the palm, in
        /// units that are square on the frame. This is the height the talisman
        /// is drawn at, so the paper is sized by the hand holding it: move the
        /// hand nearer the lens and the rune grows with it, instead of staying a
        /// fixed rectangle that looks right at one distance only.
        /// </summary>
        /// <remarks>
        /// The depth axis is included, because a hand pointing at the lens has
        /// most of its length along z and would otherwise measure as almost
        /// nothing on screen. It is weighted rather than taken at face value:
        /// depth is the noisiest number the tracker reports, so at a weight of
        /// one the paper visibly pulses whenever the hand tilts. Zero measures
        /// the rune against the hand as it appears on screen; one measures it
        /// against the hand as it stands in space.
        /// </remarks>
        public static float CalculateFingerSpan(
            HandData hand, float widthToHeight, float depthWeight = 0.5f)
        {
            var indexTip = GetPosition(hand, HandLandmarkIndex.IndexTip);
            var middleTip = GetPosition(hand, HandLandmarkIndex.MiddleTip);
            var tips = (indexTip + middleTip) * 0.5f;
            var palm = CalculatePalmCenter(hand);

            // Depth is normalised like x, so it needs the same stretch to sit on
            // the same scale as y before it can be part of the length.
            var squared = SquareDirection(tips - palm, widthToHeight);
            var weight = Mathf.Clamp01(depthWeight);
            var flat = new Vector2(squared.x, squared.y);
            var depth = squared.z * weight;
            return Mathf.Max(
                new Vector3(flat.x, flat.y, depth).magnitude, MinimumScale);
        }

        public static float NormalizedDistance(
            HandData hand,
            HandLandmarkIndex first,
            HandLandmarkIndex second)
        {
            return Vector3.Distance(GetPosition(hand, first), GetPosition(hand, second)) /
                   CalculatePalmWidth(hand);
        }
    }
}

