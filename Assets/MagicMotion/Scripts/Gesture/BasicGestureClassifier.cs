using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Gesture
{
    public sealed class BasicGestureClassifier
    {
        private readonly FingerAnalyzer fingerAnalyzer;
        private readonly float fistTipDistance;

        public BasicGestureClassifier(float extendedAngle = 145f, float fistTipDistance = 1.35f)
        {
            fingerAnalyzer = new FingerAnalyzer(extendedAngle);
            this.fistTipDistance = Mathf.Max(0.1f, fistTipDistance);
        }

        public GestureClassification Classify(HandData hand, float widthToHeight)
        {
            if (hand == null || !hand.IsValid)
            {
                return new GestureClassification(GestureType.Unknown, 0f, default);
            }

            var fingers = fingerAnalyzer.Analyze(hand, widthToHeight);
            // The four long fingers define the open palm. During a forward push
            // the thumb often appears bent in perspective, so requiring all five
            // fingers to be perfectly straight made valid casts flicker Unknown.
            if (fingers.LongFingersExtended && fingers.Thumb != FingerState.Closed)
            {
                return new GestureClassification(GestureType.OpenPalm, hand.Confidence, fingers);
            }

            if (fingers.ExtendedCount == 0 && FingertipsNearPalm(hand, widthToHeight))
            {
                return new GestureClassification(GestureType.Fist, hand.Confidence, fingers);
            }

            return new GestureClassification(GestureType.Unknown, hand.Confidence, fingers);
        }

        private bool FingertipsNearPalm(HandData hand, float widthToHeight)
        {
            var center = HandGeometry.CalculatePalmCenter(hand);
            var scale = HandGeometry.CalculateSquarePalmWidth(hand, widthToHeight);
            var tips = new[]
            {
                HandLandmarkIndex.ThumbTip,
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip,
                HandLandmarkIndex.RingTip,
                HandLandmarkIndex.PinkyTip
            };

            for (var index = 0; index < tips.Length; index++)
            {
                var distance = HandGeometry.SquareDirection(
                    HandGeometry.GetPosition(hand, tips[index]) - center, widthToHeight).magnitude / scale;
                if (distance > fistTipDistance)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

