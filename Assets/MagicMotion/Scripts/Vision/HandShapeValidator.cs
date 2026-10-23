using System.Globalization;
using UnityEngine;

namespace MagicMotion.Vision
{
    /// <summary>
    /// Rejects landmark sets that MediaPipe fitted to something that is not a
    /// hand. A face seen in profile is the common case: the model still returns
    /// 21 landmarks but the proportions of a hand no longer hold, and the
    /// skeleton ends up drawn on the head. Every test here is a ratio against
    /// the palm width, so the verdict does not depend on how close the hand is
    /// to the lens. It does still depend on the pose: nothing here may demand
    /// that the fingers be straight, because the fist and the sword finger are
    /// the poses the game is played in.
    /// </summary>
    /// <remarks>
    /// Ratios are measured in square units, see SquareDistance. Normalized
    /// coordinates are not square on a 16:9 frame, and the reference phone
    /// reports the frame sideways in portrait and standing up in landscape, so
    /// the old normalized measurements made one whole orientation fail bounds
    /// that were taken in the other one: in landscape wristToIndexMcp measured
    /// 2.2 against a limit of 1.8 and every single hand was thrown away.
    /// </remarks>
    public static class HandShapeValidator
    {
        private const float MinimumPalmWidth = 0.0001f;

        // These are the bounds as measured in square units. They were taken in
        // portrait, where the hand lies sideways in the frame and every long
        // measurement runs along x, so each one is the normalized bound it
        // replaced times the 1280 / 720 aspect of the frame it was taken on.
        //
        // The three floors were measured on an open hand, and the game's
        // gestures are not open hands: the fist, the sword finger and the two
        // finger sign all curl the middle finger, which pulls Wrist to
        // MiddleTip down from the 2.4 to 3.1 of a spread hand to 0.9 to 1.4,
        // and tucks the thumb tip in towards the index knuckle. Measured on
        // the reference phone, every one of those poses sat under the old
        // floors in both portrait and landscape, so the floors follow the most
        // curled pose seen instead of the most open one. The ceilings are what
        // keeps a face out and none of them moved.
        private const float MinimumWristToMiddleTip = 0.6f;
        private const float MaximumWristToMiddleTip = 8f;
        private const float MinimumWristToIndexMcp = 0.6222f;
        private const float MaximumWristToIndexMcp = 3.2f;
        private const float MinimumThumbToIndexMcp = 0.25f;
        private const float MaximumThumbToIndexMcp = 5.3333f;
        private const float MaximumWristToTip = 8.8889f;
        private const float MinimumBoxAspect = 0.35f;
        private const float MaximumBoxAspect = 7.1111f;

        public static bool IsPlausibleHand(HandData hand, float widthToHeight)
        {
            if (hand == null)
            {
                return false;
            }

            if (!hand.IsValid)
            {
                return false;
            }

            var palm = CalculatePalmWidth(hand, widthToHeight);
            if (palm <= MinimumPalmWidth)
            {
                return false;
            }

            var wristToMiddleTip = SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.MiddleTip, widthToHeight) / palm;
            if (wristToMiddleTip < MinimumWristToMiddleTip)
            {
                return false;
            }

            if (wristToMiddleTip > MaximumWristToMiddleTip)
            {
                return false;
            }

            var wristToIndexMcp = SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.IndexMcp, widthToHeight) / palm;
            if (wristToIndexMcp < MinimumWristToIndexMcp)
            {
                return false;
            }

            if (wristToIndexMcp > MaximumWristToIndexMcp)
            {
                return false;
            }

            var thumbToIndexMcp = SquareDistance(
                hand, HandLandmarkIndex.ThumbTip, HandLandmarkIndex.IndexMcp, widthToHeight) / palm;
            if (thumbToIndexMcp < MinimumThumbToIndexMcp)
            {
                return false;
            }

            if (thumbToIndexMcp > MaximumThumbToIndexMcp)
            {
                return false;
            }

            if (HasTipTooFarFromWrist(hand, palm, widthToHeight))
            {
                return false;
            }

            var aspect = CalculateBoxAspect(hand, widthToHeight);
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

        /// <summary>
        /// Every ratio the verdict is taken on, plus the landmark box, all in
        /// the square units the bounds above are written in, so the numbers can
        /// be read straight off logcat against those bounds.
        /// </summary>
        public static string DescribeMeasurements(HandData hand, float widthToHeight)
        {
            if (hand == null || !hand.IsValid)
            {
                return "invalid hand";
            }

            var palm = CalculatePalmWidth(hand, widthToHeight);
            var wristToMiddleTip = SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.MiddleTip, widthToHeight) / palm;
            var wristToIndexMcp = SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.IndexMcp, widthToHeight) / palm;
            var thumbToIndexMcp = SquareDistance(
                hand, HandLandmarkIndex.ThumbTip, HandLandmarkIndex.IndexMcp, widthToHeight) / palm;
            var maximumWristToTip = SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.IndexTip, widthToHeight) / palm;
            maximumWristToTip = Mathf.Max(maximumWristToTip, wristToMiddleTip);
            maximumWristToTip = Mathf.Max(maximumWristToTip, SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.RingTip, widthToHeight) / palm);
            maximumWristToTip = Mathf.Max(maximumWristToTip, SquareDistance(
                hand, HandLandmarkIndex.Wrist, HandLandmarkIndex.PinkyTip, widthToHeight) / palm);

            var minimumX = float.MaxValue;
            var maximumX = float.MinValue;
            var minimumY = float.MaxValue;
            var maximumY = float.MinValue;
            for (var index = 0; index < hand.Landmarks.Count; index++)
            {
                var position = hand.Landmarks[index].Position;
                minimumX = Mathf.Min(minimumX, position.x);
                maximumX = Mathf.Max(maximumX, position.x);
                minimumY = Mathf.Min(minimumY, position.y);
                maximumY = Mathf.Max(maximumY, position.y);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "palm={0:0.0000} w2mt={1:0.000} w2im={2:0.000} t2im={3:0.000} " +
                "maxTip={4:0.000} aspect={5:0.000} box={6:0.00}/{7:0.00}/{8:0.00}/{9:0.00}",
                palm, wristToMiddleTip, wristToIndexMcp, thumbToIndexMcp, maximumWristToTip,
                CalculateBoxAspect(hand, widthToHeight),
                minimumX, minimumY, maximumX, maximumY);
        }

        /// <summary>
        /// Distance in units that are square on the frame. Normalized x spans
        /// width pixels and y spans height pixels, so on a 16:9 frame one unit
        /// of x covers almost twice as much of the hand as one unit of y, and a
        /// ratio between two of these distances changes with the way the hand
        /// happens to lie in the frame. Stretching x by width / height puts
        /// both axes on one scale, which is what makes a single set of bounds
        /// hold for every orientation.
        /// </summary>
        private static float SquareDistance(
            HandData hand,
            HandLandmarkIndex first,
            HandLandmarkIndex second,
            float widthToHeight)
        {
            var from = HandGeometry.GetPosition(hand, first);
            var to = HandGeometry.GetPosition(hand, second);
            var dx = (to.x - from.x) * widthToHeight;
            var dz = (to.z - from.z) * widthToHeight;
            var dy = to.y - from.y;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float CalculatePalmWidth(HandData hand, float widthToHeight)
        {
            return Mathf.Max(
                SquareDistance(
                    hand, HandLandmarkIndex.IndexMcp, HandLandmarkIndex.PinkyMcp, widthToHeight),
                MinimumPalmWidth);
        }

        private static bool HasTipTooFarFromWrist(HandData hand, float palm, float widthToHeight)
        {
            var tips = new[]
            {
                HandLandmarkIndex.IndexTip,
                HandLandmarkIndex.MiddleTip,
                HandLandmarkIndex.RingTip,
                HandLandmarkIndex.PinkyTip
            };

            for (var index = 0; index < tips.Length; index++)
            {
                if (SquareDistance(hand, HandLandmarkIndex.Wrist, tips[index], widthToHeight) /
                    palm > MaximumWristToTip)
                {
                    return true;
                }
            }

            return false;
        }

        private static float CalculateBoxAspect(HandData hand, float widthToHeight)
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

            var width = (maximumX - minimumX) * widthToHeight;
            var height = maximumY - minimumY;
            if (height <= MinimumPalmWidth)
            {
                return float.MaxValue;
            }

            return width / height;
        }
    }
}
