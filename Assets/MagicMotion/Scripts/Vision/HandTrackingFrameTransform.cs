using UnityEngine;

namespace MagicMotion.Vision
{
    public static class HandTrackingFrameTransform
    {
        public static int NormalizeRotation(int degrees)
        {
            var normalized = ((degrees % 360) + 360) % 360;
            return normalized switch
            {
                < 45 => 0, < 135 => 90, < 225 => 180, < 315 => 270, _ => 0
            };
        }

        public static void GetMediaPipeInputFlips(bool frontFacing, bool verticallyMirrored,
            int rotationDegrees, out bool flipHorizontally, out bool flipVertically)
        {
            var rotation = NormalizeRotation(rotationDegrees);
            var inverted = rotation == 90 || rotation == 270;
            flipHorizontally = !inverted && frontFacing;
            flipVertically = !frontFacing
                ? !verticallyMirrored
                : inverted ? verticallyMirrored : !verticallyMirrored;
        }

        public static Rect GetDisplayUv(bool frontFacing, bool verticallyMirrored, int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);
            var uv = new Rect(0f, 0f, 1f, 1f);
            if (verticallyMirrored) uv = FlipVertically(uv);
            if (frontFacing)
            {
                uv = rotation == 0 || rotation == 180 ? FlipHorizontally(uv) : FlipVertically(uv);
            }
            return uv;
        }

        public static Vector2 LandmarkToGuiNormalized(Vector3 internalLandmark,
            int rotationDegrees, bool mirrored)
        {
            var normalizedX = internalLandmark.x;
            var normalizedY = 1f - internalLandmark.y;
            var annotationRotation = NormalizeRotation(-rotationDegrees);
            var inverted = annotationRotation == 90 || annotationRotation == 270;
            var nx = inverted ? normalizedY : normalizedX;
            var ny = inverted ? normalizedX : normalizedY;
            var x = IsXReversed(annotationRotation, mirrored) ? 1f - nx : nx;
            var unityY = IsYReversed(annotationRotation, mirrored) ? 1f - ny : ny;
            return new Vector2(x, 1f - unityY);
        }

        private static bool IsXReversed(int rotation, bool mirrored) => mirrored
            ? rotation == 0 || rotation == 90
            : rotation == 90 || rotation == 180;

        private static bool IsYReversed(int rotation, bool mirrored) => mirrored
            ? rotation == 0 || rotation == 270
            : rotation == 0 || rotation == 90;

        private static Rect FlipHorizontally(Rect rect) =>
            new Rect(1f - rect.x, rect.y, -rect.width, rect.height);

        private static Rect FlipVertically(Rect rect) =>
            new Rect(rect.x, 1f - rect.y, rect.width, -rect.height);
    }
}
