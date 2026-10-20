using UnityEngine;

namespace MagicMotion.UI
{
    public static class HandTrackingOverlayLayout
    {
        public static Rect Fit(Rect viewport, int sourceWidth, int sourceHeight, int rotationDegrees)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || viewport.width <= 0f || viewport.height <= 0f)
            {
                return viewport;
            }

            var rotation = NormalizeRotation(rotationDegrees);
            var displayWidth = rotation == 90 || rotation == 270 ? sourceHeight : sourceWidth;
            var displayHeight = rotation == 90 || rotation == 270 ? sourceWidth : sourceHeight;
            var scale = Mathf.Min(viewport.width / displayWidth, viewport.height / displayHeight);
            var width = displayWidth * scale;
            var height = displayHeight * scale;
            return new Rect(
                viewport.center.x - width * 0.5f,
                viewport.center.y - height * 0.5f,
                width,
                height);
        }

        public static Vector2 NormalizedToGui(Rect content, Vector3 normalizedLandmark)
        {
            return new Vector2(
                content.xMin + Mathf.Clamp01(normalizedLandmark.x) * content.width,
                content.yMax - Mathf.Clamp01(normalizedLandmark.y) * content.height);
        }

        public static int NormalizeRotation(int rotationDegrees)
        {
            var normalized = ((rotationDegrees % 360) + 360) % 360;
            return normalized switch
            {
                < 45 => 0,
                < 135 => 90,
                < 225 => 180,
                < 315 => 270,
                _ => 0
            };
        }
    }
}
