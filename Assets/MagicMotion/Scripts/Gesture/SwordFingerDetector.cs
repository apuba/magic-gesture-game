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

        public bool IsSwordFinger(HandData hand, float widthToHeight)
        {
            if (hand == null || !hand.IsValid)
            {
                return false;
            }

            var fingers = fingerAnalyzer.Analyze(hand, widthToHeight);
            if (fingers.Index != FingerState.Extended ||
                fingers.Middle != FingerState.Extended ||
                fingers.Ring == FingerState.Extended ||
                fingers.Pinky == FingerState.Extended ||
                fingers.Thumb == FingerState.Extended)
            {
                return false;
            }

            var palm = HandGeometry.CalculateSquarePalmWidth(hand, widthToHeight);
            return HandGeometry.SquareDistance(
                hand,
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip,
                widthToHeight) / palm <= maximumTipSeparation;
        }

        /// <summary>
        /// The two-finger pose used by the visible rune. It keeps the actual
        /// sword-finger constraints, but intentionally does not make the
        /// thumb a gate: it is frequently inferred as straight while the hand
        /// back faces the camera. This is visual feedback only; future spell
        /// sequencing can keep a stricter confirmation rule if needed.
        /// </summary>
        public bool IsRuneFinger(HandData hand, float widthToHeight)
        {
            if (hand == null || !hand.IsValid)
            {
                return false;
            }

            var fingers = fingerAnalyzer.Analyze(hand, widthToHeight);
            if (fingers.Index != FingerState.Extended ||
                fingers.Middle != FingerState.Extended ||
                fingers.Ring == FingerState.Extended ||
                fingers.Pinky == FingerState.Extended)
            {
                return false;
            }

            var palm = HandGeometry.CalculateSquarePalmWidth(hand, widthToHeight);
            return HandGeometry.SquareDistance(
                hand,
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip,
                widthToHeight) / palm <= maximumTipSeparation;
        }

        // Exposed so a caller can report which finger is actually deciding a
        // verdict, instead of only the verdict.
        public FingerAnalysis Analyze(HandData hand, float widthToHeight)
        {
            return fingerAnalyzer.Analyze(hand, widthToHeight);
        }

        // Accepts both the two-finger sword pose and a single pointing index,
        // so the rune trail can start from either.
        //
        // The thumb is deliberately left out of the verdict. Turning the back
        // of the hand towards the camera hides it, and the landmarks are then
        // inferred from the palm side, which put both of its joint angles right
        // on the extended threshold: measured on the reference phone in
        // landscape it flipped between Bent and Extended every few frames while
        // the index, the ring and the pinky never moved once, and the trail and
        // the rune meter followed it. It decides nothing the ring and the pinky
        // do not already decide, since an open hand is refused by those two
        // whether or not the thumb can be seen.
        public bool IsPointing(HandData hand, float widthToHeight)
        {
            if (hand == null)
            {
                return false;
            }

            if (!hand.IsValid)
            {
                return false;
            }

            var fingers = fingerAnalyzer.Analyze(hand, widthToHeight);
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
