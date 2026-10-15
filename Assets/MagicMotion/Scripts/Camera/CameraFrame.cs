using UnityEngine;

namespace MagicMotion.Camera
{
    public readonly struct CameraFrame
    {
        public CameraFrame(
            WebCamTexture texture,
            long sequence,
            double timestamp,
            int rotationDegrees,
            bool verticallyMirrored,
            bool frontFacing)
        {
            Texture = texture;
            Sequence = sequence;
            Timestamp = timestamp;
            RotationDegrees = rotationDegrees;
            VerticallyMirrored = verticallyMirrored;
            FrontFacing = frontFacing;
        }

        public WebCamTexture Texture { get; }
        public long Sequence { get; }
        public double Timestamp { get; }
        public int RotationDegrees { get; }
        public bool VerticallyMirrored { get; }
        public bool FrontFacing { get; }
        public bool IsValid => Texture != null && Texture.width > 16 && Texture.height > 16;
    }
}

