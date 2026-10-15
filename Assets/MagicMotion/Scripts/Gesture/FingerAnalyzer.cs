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

        public FingerAnalysis Analyze(HandData hand)
        {
            return new FingerAnalysis(
                AnalyzeThumb(hand),
                AnalyzeFinger(hand, HandLandmarkIndex.IndexMcp, HandLandmarkIndex.IndexPip,
                    HandLandmarkIndex.IndexDip, HandLandmarkIndex.IndexTip),
                AnalyzeFinger(hand, HandLandmarkIndex.MiddleMcp, HandLandmarkIndex.MiddlePip,
                    HandLandmarkIndex.MiddleDip, HandLandmarkIndex.MiddleTip),
                AnalyzeFinger(hand, HandLandmarkIndex.RingMcp, HandLandmarkIndex.RingPip,
                    HandLandmarkIndex.RingDip, HandLandmarkIndex.RingTip),
                AnalyzeFinger(hand, HandLandmarkIndex.PinkyMcp, HandLandmarkIndex.PinkyPip,
                    HandLandmarkIndex.PinkyDip, HandLandmarkIndex.PinkyTip));
        }

        private FingerState AnalyzeThumb(HandData hand)
        {
            var cmc = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbCmc);
            var mcp = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbMcp);
            var ip = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbIp);
            var tip = HandGeometry.GetPosition(hand, HandLandmarkIndex.ThumbTip);
            var mcpAngle = JointAngle(cmc, mcp, ip);
            var ipAngle = JointAngle(mcp, ip, tip);
            return Classify(mcpAngle, ipAngle);
        }

        private FingerState AnalyzeFinger(
            HandData hand,
            HandLandmarkIndex mcpIndex,
            HandLandmarkIndex pipIndex,
            HandLandmarkIndex dipIndex,
            HandLandmarkIndex tipIndex)
        {
            var mcp = HandGeometry.GetPosition(hand, mcpIndex);
            var pip = HandGeometry.GetPosition(hand, pipIndex);
            var dip = HandGeometry.GetPosition(hand, dipIndex);
            var tip = HandGeometry.GetPosition(hand, tipIndex);
            return Classify(JointAngle(mcp, pip, dip), JointAngle(pip, dip, tip));
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

        private static float JointAngle(Vector3 previous, Vector3 joint, Vector3 next)
        {
            var towardPrevious = previous - joint;
            var towardNext = next - joint;
            if (towardPrevious.sqrMagnitude < 0.0000001f || towardNext.sqrMagnitude < 0.0000001f)
            {
                return 0f;
            }

            return Vector3.Angle(towardPrevious, towardNext);
        }
    }
}

