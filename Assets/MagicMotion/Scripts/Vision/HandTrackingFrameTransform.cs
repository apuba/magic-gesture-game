using UnityEngine;


namespace MagicMotion.Vision
{
    /// <summary>
    /// Defines the single camera-frame convention shared by MediaPipe input and
    /// the debug overlay. The formulas mirror MediaPipeUnityPlugin's
    /// ImageTransformationOptions and ImageCoordinate helpers.
    /// </summary>
    public static class HandTrackingFrameTransform
    {
        public static int NormalizeRotation(int degrees)
        {
            var normalized = ((degrees % 360) + 360) % 360;
            return normalized switch
            {
                < 45 => 0,
                < 135 => 90,
                < 225 => 180,
                < 315 => 270,
                _ => 0
            };
        }

        public static void GetMediaPipeInputFlips(
            bool frontFacing,
            bool verticallyMirrored,
            int rotationDegrees,
            out bool flipHorizontally,
            out bool flipVertically)
        {
            var rotation = NormalizeRotation(rotationDegrees);
            var inverted = rotation == 90 || rotation == 270;
            flipHorizontally = !inverted && frontFacing;
            flipVertically = !frontFacing
                ? !verticallyMirrored
                : inverted ? verticallyMirrored : !verticallyMirrored;
        }

        public static Rect GetDisplayUv(
            bool frontFacing,
            bool verticallyMirrored,
            int rotationDegrees)
        {
            // Measured on the reference phone: the picture only comes out the
            // right way up when the front-facing frame is mirrored the same way
            // in portrait and in landscape, so the rotation decides nothing here.
            var uv = new Rect(0f, 0f, 1f, 1f);
            if (verticallyMirrored)
            {
                uv = FlipVertically(uv);
            }

            if (frontFacing)
            {
                uv = FlipHorizontally(uv);
            }

            return uv;
        }

        public static Vector2 LandmarkToGuiNormalized(
            Vector3 internalLandmark,
            int rotationDegrees,
            bool mirrored)
        {
            // HandData stores y-up while both the tracker image and the GUI use
            // y-down, so the vertical axis only needs restoring here.
            var normalizedX = internalLandmark.x;
            var normalizedY = 1f - internalLandmark.y;
            var rotation = NormalizeRotation(rotationDegrees);
            var inverted = rotation == 90 || rotation == 270;
            var point = new Vector2(inverted ? 1f - normalizedX : normalizedX, normalizedY);

            // Measured on the reference phone: on top of that restore the
            // skeleton only sits wrong by a quarter turn, and only in portrait,
            // which also needs a horizontal mirror afterwards. Landscape already
            // lines up. Rear cameras are not calibrated yet.
            var turn = rotation switch
            {
                90 => 270,
                270 => 90,
                _ => 180
            };

            point = RotateClockwise(point, turn);
            if (inverted)
            {
                point.x = 1f - point.x;
            }

            return point;
        }

        private static Vector2 RotateClockwise(Vector2 point, int degrees)
        {
            var angle = NormalizeRotation(degrees) * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var dx = point.x - 0.5f;
            var dy = point.y - 0.5f;
            return new Vector2(0.5f + dx * cos - dy * sin, 0.5f + dx * sin + dy * cos);
        }

        private static Rect FlipHorizontally(Rect rect)
        {
            return new Rect(1f - rect.x, rect.y, -rect.width, rect.height);
        }

        private static Rect FlipVertically(Rect rect)
        {
            return new Rect(rect.x, 1f - rect.y, rect.width, -rect.height);
        }
    }
}