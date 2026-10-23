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

            // Portrait runs on rotationDegrees alone, so the frame the tracker
            // reads is already in the sensor's own orientation and that one
            // rotation is all it takes to stand it up. The old rule flipped
            // vertically whenever the frame was not reported mirrored, which
            // only ever fired for 0 and 180, so both landscape orientations
            // went in upside down and the palm detector lost a still hand
            // almost every frame. Keep the vertical flip tied to the frame.
            flipVertically = !frontFacing ? !verticallyMirrored : verticallyMirrored;
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

        /// <summary>
        /// Maps a landmark from the tracker image onto the rect the overlay
        /// draws by repeating exactly what the overlay does to the picture: the
        /// two GetDisplayUv flips first, then the turn DrawCorrectedCamera
        /// makes with GUI.matrix. The previous rule only ever applied half or
        /// quarter turns inferred from one phone, so 180 collapsed onto 0 and
        /// the landscape orientation that reports 180 ended up a half turn out.
        /// </summary>
        public static Vector2 LandmarkToGuiNormalized(
            Vector3 internalLandmark,
            int rotationDegrees,
            bool mirrored,
            bool verticallyMirrored)
        {
            // HandData stores y-up while both the tracker image and the GUI use
            // y-down, so the vertical axis only needs restoring here.
            var x = internalLandmark.x;
            var y = 1f - internalLandmark.y;

            // The camera frame reaches the GPU the other way up from the pixel
            // array MediaPipe read its landmarks from, and GetDisplayUv flips
            // the picture again when the frame is reported vertically mirrored,
            // so the two only cancel out in that one case. Measured on the
            // reference phone: rotation=270, mirrorV=False in portrait.
            if (!verticallyMirrored)
            {
                y = 1f - y;
            }

            if (mirrored)
            {
                x = 1f - x;
            }

            var rotation = NormalizeRotation(rotationDegrees);
            var point = rotation switch
            {
                90 => new Vector2(y, 1f - x),
                180 => new Vector2(1f - x, 1f - y),
                270 => new Vector2(1f - y, x),
                _ => new Vector2(x, y)
            };

            // GetDisplayUv mirrors the front camera inside the frame, before the
            // turn, but the mirror the player reads is the one across the
            // screen, and the turn does not carry one onto the other: at 0 and
            // 180 the mirrored frame lands with its x axis pointing the other
            // way. Measured on the reference phone: both landscape orientations
            // need the mirror applied across the screen, portrait is already
            // right the way it is.
            if (mirrored && (rotation == 0 || rotation == 180))
            {
                point.x = 1f - point.x;
            }

            return point;
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