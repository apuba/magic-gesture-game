using MagicMotion.Vision;

namespace MagicMotion.Gesture
{
    /// <summary>
    /// Reduces the raw palm normal of a tracking result to the facing the
    /// summon sequence gates on.
    /// </summary>
    /// <remarks>
    /// The classifier it wraps already aims the comparison at the lens instead
    /// of a fixed screen axis, so a hand held off to one side of the frame is
    /// judged against the direction it is actually seen from. The two up and
    /// down readings are deliberately folded into <see cref="HandFacing.Unknown"/>:
    /// they are not part of the back-to-palm turn, and a hand pointing its
    /// fingers at the ceiling or the floor must not be mistaken for a stage of
    /// it.
    /// </remarks>
    public sealed class HandFacingDetector
    {
        private readonly PalmDirectionClassifier classifier;

        public HandFacingDetector(float minimumAxisAlignment = 0.45f)
        {
            classifier = new PalmDirectionClassifier(minimumAxisAlignment);
        }

        public HandFacing Detect(HandData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                return HandFacing.Unknown;
            }

            return classifier.Classify(hand) switch
            {
                PalmDirection.Forward => HandFacing.PalmToCamera,
                PalmDirection.Backward => HandFacing.BackToCamera,
                PalmDirection.Left => HandFacing.SideToCamera,
                PalmDirection.Right => HandFacing.SideToCamera,
                _ => HandFacing.Unknown
            };
        }
    }
}
