using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Gesture
{
    public sealed class FingerAnalyzer
    {
        private readonly float extendedAngle;

        public FingerAnalyzer(float extendedAngle = 155f)
        {
            extendedAngle = Mathf.Clamp(extendedAngle, 120f, 179f);
            this.extendedAngle = extendedAngle;
        }

        /// <summary>
        /// widthToHeight is the shape of the frame the landmarks were taken on.
        /// A joint angle is the angle between two offsets, and in normalized
        /// coordinates those offsets are squashed differently along x and y, so
        /// on a 16:9 frame the same finger measures up to about fifteen degrees
        /// straighter or more bent depending on which way it lies in the
        /// frame. That straddled the extended threshold and flipped the state
        /// of a finger every time the phone turned, which is what made the
        /// trail jump between the fingertip and the palm.
        /// </summary>
        public FingerAnalysis Analyze(HandData hand, float widthToHeight)
        {
            return new FingerAnalysis(
                AnalyzeThumb(hand, widthToHeight),
                AnalyzeFinger(hand, widthToHeight, HandLandmarkIndex.IndexMcp,
                    HandLandmarkIndex.IndexPip, HandLandmarkIndex.IndexDip, HandLandmarkIndex.IndexTip),
                AnalyzeFinger(hand, widthToHeight, HandLandmarkIndex.MiddleMcp,
                    HandLandmarkIndex.MiddlePip, HandLandmarkIndex.MiddleDip, HandLandmarkIndex.MiddleTip),
                AnalyzeFinger(hand, widthToHeight, HandLandmarkIndex.RingMcp,
                    HandLandmarkIndex.RingPip, HandLandmarkIndex.RingDip, HandLandmarkIndex.RingTip),
                AnalyzeFinger(hand, widthToHeight, HandLandmarkIndex.PinkyMcp,
                    HandLandmarkIndex.PinkyPip, HandLandmarkIndex.PinkyDip, HandLandmarkIndex.PinkyTip));
        }

        private FingerState AnalyzeThumb(HandData hand, float widthToHeight)
        {
            var cmc = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbCmc);
            var mcp = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbMcp);
            var ip = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbIp);
            var tip = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbTip);
            var mcpAngle = JointAngle(cmc, mcp, ip, widthToHeight);
            var ipAngle = JointAngle(mcp, ip, tip, widthToHeight);
            return Classify(mcpAngle, ipAngle);
        }

        private FingerState AnalyzeFinger(
            HandData hand,
            float widthToHeight,
            HandLandmarkIndex mcpIndex,
            HandLandmarkIndex pipIndex,
            HandLandmarkIndex dipIndex,
            HandLandmarkIndex tipIndex)
        {
            var mcp = HandGeometry.GetPosition(hand, mcpIndex);
            var pip = HandGeometry.GetPosition(hand, pipIndex);
            var dip = HandGeometry.GetPosition(hand, dipIndex);
            var tip = HandGeometry.GetPosition(hand, tipIndex);
            return Classify(
                JointAngle(mcp, pip, dip, widthToHeight),
                JointAngle(pip, dip, tip, widthToHeight));
        }

        private FingerState Classify(float firstAngle, float secondAngle)
        {
            if (firstAngle >= extendedAngle && secondAngle >= extendedAngle)
            {
                return FingerState.Extended;
            }

            if (firstAngle < 95f || secondAngle < 95f)
            {
                return FingerState.Closed;
            }

            return FingerState.Bent;
        }

        private static float JointAngle(
            Vector3 previous, Vector3 joint, Vector3 next, float widthToHeight)
        {
            var towardPrevious = HandGeometry.SquareDirection(previous - joint, widthToHeight);
            var towardNext = HandGeometry.SquareDirection(next - joint, widthToHeight);
            if (towardPrevious.sqrMagnitude < 0.0000001f || towardNext.sqrMagnitude < 0.0000001f)
            {
                return 0f;
            }

            return Vector3.Angle(towardPrevious, towardNext);
        }
    }
}
