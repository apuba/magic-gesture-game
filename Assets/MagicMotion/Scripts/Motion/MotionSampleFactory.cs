using MagicMotion.Vision;

namespace MagicMotion.Motion
{
    public static class MotionSampleFactory
    {
        private static readonly HandLandmarkIndex[] PalmLandmarks =
        {
            HandLandmarkIndex.Wrist,
            HandLandmarkIndex.IndexMcp,
            HandLandmarkIndex.MiddleMcp,
            HandLandmarkIndex.RingMcp,
            HandLandmarkIndex.PinkyMcp
        };

        public static HandMotionSample Create(HandData hand, double timestamp)
        {
            var depth = 0f;
            for (var index = 0; index < PalmLandmarks.Length; index++)
            {
                depth += HandGeometry.GetPosition(hand, PalmLandmarks[index]).z;
            }

            return new HandMotionSample(
                timestamp,
                HandGeometry.CalculatePalmCenter(hand),
                HandGeometry.CalculatePalmWidth(hand),
                depth / PalmLandmarks.Length);
        }
    }
}

