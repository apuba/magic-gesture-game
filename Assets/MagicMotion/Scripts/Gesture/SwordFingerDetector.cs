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
                fingers.Ring != FingerState.Closed ||
                fingers.Pinky != FingerState.Closed ||
                fingers.Thumb == FingerState.Extended)
            {
                return false;
            }

            return HandGeometry.NormalizedDistance(
                hand,
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip) <= maximumTipSeparation;
        }

        public static Vector3 CalculateBrushPoint(HandData hand)
        {
            return (HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip) +
                    HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleTip)) * 0.5f;
        }
    }
}
