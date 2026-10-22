using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Gesture
{
    public sealed class SwordFingerDetector
    {
        private readonly FingerAnalyzer fingerAnalyzer;
        private readonly float maximumTipSeparation;

        public SwordFingerDetector(float extendedAngle = 145f, float maximumTipSeparation = 0.42f)
        {
            fingerAnalyzer = new FingerAnalyzer(extendedAngle);
            this.maximumTipSeparation = Mathf.Max(0.05f, maximumTipSeparation);
        }

        public bool IsSwordFinger(HandData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                return false;
            }

            var fingers = fingerAnalyzer.Analyze(hand);
            if (fingers.Index != FingerState.Extended ||
                fingers.Middle != FingerState.Extended ||
                fingers.Ring == FingerState.Extended ||
                fingers.Pinky == FingerState.Extended ||
                fingers.Thumb == FingerState.Extended)
            {
                return false;
            }

            return HandGeometry.NormalizedDistance(
                hand,
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip) <= maximumTipSeparation;
        }

        // Accepts both the two-finger sword pose and a single pointing index,
        // so the rune trail can start from either.
        public bool IsPointing(HandData hand)
        {
            if (hand == null)
            {
                return false;
            }

            if (!hand.IsValid)
            {
                return false;
            }

            var fingers = fingerAnalyzer.Analyze(hand);
            if (fingers.Index != FingerState.Extended)
            {
                return false;
            }

            if (fingers.Ring == FingerState.Extended)
            {
                return false;
            }

            if (fingers.Pinky == FingerState.Extended)
            {
                return false;
            }

            if (fingers.Thumb == FingerState.Extended)
            {
                return false;
            }

            return true;
        }

        public static Vector3 CalculateBrushPoint(HandData hand)
        {
                        // The rune is drawn with the index fingertip so the trail follows
            // the finger the player points with, not the middle of the hand.
            return HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip);
        }
    }
}
