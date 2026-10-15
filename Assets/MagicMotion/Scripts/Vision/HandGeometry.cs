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

