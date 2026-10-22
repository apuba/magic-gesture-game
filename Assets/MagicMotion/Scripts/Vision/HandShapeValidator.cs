using UnityEngine;

namespace MagicMotion.Vision
{
    /// <summary>
    /// Rejects landmark sets that MediaPipe fitted to something that is not a
    /// hand. A face seen in profile is the common case: the model still returns
    /// 21 landmarks but the proportions of a hand no longer hold, and the
    /// skeleton ends up drawn on the head. Every test here is a ratio against
    /// the palm width, so the verdict does not depend on how close the hand is
    /// to the lens, and the bounds stay loose enough to keep fists valid.
    /// </summary>
    public static class HandShapeValidator
    {
        private const float MinimumPalmWidth = 0.0001f;
        private const float MinimumWristToMiddleTip = 0.8f;
        private const float MaximumWristToMiddleTip = 4.5f;
        private const float MinimumWristToIndexMcp = 0.35f;
        private const float MaximumWristToIndexMcp = 1.8f;
        private const float MinimumThumbToIndexMcp = 0.2f;
        private const float MaximumThumbToIndexMcp = 3f;
        private const float MaximumWristToTip = 5f;
        private const float MinimumBoxAspect = 0.25f;
        private const float MaximumBoxAspect = 4f;

        public static bool IsPlausibleHand(HandData hand)
        {
            if (hand == null)
            {
                return false;
            }

            if (!hand.IsValid)
            {
                return false;
            }

            var palm = HandGeometry.CalculatePalmWidth(hand);
            if (palm <= MinimumPalmWidth)
            {
                return false;
            }

            var wristToMiddleTip = HandGeometry.NormalizedDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.MiddleTip);
            if (wristToMiddleTip < MinimumWristToMiddleTip)
            {
                return false;
            }

            if (wristToMiddleTip > MaximumWristToMiddleTip)
            {
                return false;
            }

            var wristToIndexMcp = HandGeometry.NormalizedDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.IndexMcp);
            if (wristToIndexMcp < MinimumWristToIndexMcp)
            {
                return false;
            }

            if (wristToIndexMcp > MaximumWristToIndexMcp)
            {
                return false;
            }

            var thumbTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbTip);
            var indexMcp = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexMcp);
            var thumbToIndexMcp = Vector3.Distance(thumbTip, indexMcp) / palm;
            if (thumbToIndexMcp < MinimumThumbToIndexMcp)
            {
                return false;
            }

            if (thumbToIndexMcp > MaximumThumbToIndexMcp)
            {
                return false;
            }

            if (HasTipTooFarFromWrist(hand, palm))
            {
                return false;
            }

            var aspect = CalculateBoxAspect(hand);
            if (aspect < MinimumBoxAspect)
            {
                return false;
            }

            if (aspect > MaximumBoxAspect)
            {
                return false;
            }

            return true;
        }

        private static bool HasTipTooFarFromWrist(HandData hand, float palm)
        {
            var wrist = HandGeometry.GetPosition(hand, HandLandmarkIndex.Wrist);
            var indexTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.IndexTip);
            var middleTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.MiddleTip);
            var ringTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.RingTip);
            var pinkyTip = HandGeometry.GetPosition(hand, HandLandmarkIndex.PinkyTip);

            if (Vector3.Distance(wrist, indexTip) / palm > MaximumWristToTip)
            {
                return true;
            }

            if (Vector3.Distance(wrist, middleTip) / palm > MaximumWristToTip)
            {
                return true;
            }

            if (Vector3.Distance(wrist, ringTip) / palm > MaximumWristToTip)
            {
                return true;
            }

            if (Vector3.Distance(wrist, pinkyTip) / palm > MaximumWristToTip)
            {
                return true;
            }

            return false;
        }

        private static float CalculateBoxAspect(HandData hand)
        {
            var minimumX = float.MaxValue;
            var maximumX = float.MinValue;
            var minimumY = float.MaxValue;
            var maximumY = float.MinValue;

            for (var index = 0; index < hand.Landmarks.Count; index++)
            {
                var position = hand.Landmarks[index].Position;
                if (position.x < minimumX)
                {
                    minimumX = position.x;
                }

                if (position.x > maximumX)
                {
                    maximumX = position.x;
                }

                if (position.y < minimumY)
                {
                    minimumY = position.y;
                }

                if (position.y > maximumY)
                {
                    maximumY = position.y;
                }
            }

            var width = maximumX - minimumX;
            var height = maximumY - minimumY;
            if (height <= MinimumPalmWidth)
            {
                return float.MaxValue;
            }

            return width / height;
        }
    }
}
