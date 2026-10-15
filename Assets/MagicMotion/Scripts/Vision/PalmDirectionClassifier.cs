using UnityEngine;

namespace MagicMotion.Vision
{
    public sealed class PalmDirectionClassifier
    {
        private readonly float minimumAxisAlignment;

        public PalmDirectionClassifier(float minimumAxisAlignment = 0.45f)
        {
            this.minimumAxisAlignment = Mathf.Clamp(minimumAxisAlignment, 0.1f, 1f);
        }

        public PalmDirection Classify(Vector3 palmNormal)
        {
            return Classify(palmNormal, new Vector3(0.5f, 0.5f, 0f));
        }

        public PalmDirection Classify(HandData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                return PalmDirection.Unknown;
            }

            return Classify(hand.PalmNormal, HandGeometry.CalculatePalmCenter(hand));
        }

        public PalmDirection Classify(Vector3 palmNormal, Vector3 palmCenter)
        {
            if (palmNormal.sqrMagnitude < 0.0001f)
            {
                return PalmDirection.Unknown;
            }

            var normal = palmNormal.normalized;
            var absoluteX = Mathf.Abs(normal.x);
            var absoluteY = Mathf.Abs(normal.y);
            var absoluteZ = Mathf.Abs(normal.z);

            // The palm faces the camera when its normal points toward -z. Accept
            // a generous tilt (up to ~55 degrees) so a push is not rejected just
            // because the wrist rotated a little during the motion. The normal
            // must still be pointing mostly forward, not purely sideways.
            // A palm faces the lens, not a fixed screen axis. At the image edges
            // the correct viewing direction contains horizontal and/or vertical
            // components. Approximate the ray from the palm to the optical
            // centre, then compare the normal with that ray. The 1.2 factor is a
            // conservative field-of-view approximation for phone front cameras.
            var towardLens = new Vector3(
                (0.5f - palmCenter.x) * 1.2f,
                (0.5f - palmCenter.y) * 1.2f,
                -1f).normalized;
            var forwardAlignment = Vector3.Dot(normal, towardLens);
            var backwardAlignment = Vector3.Dot(-normal, towardLens);
            if (normal.z < -0.15f && forwardAlignment >= 0.42f)
            {
                return PalmDirection.Forward;
            }

            if (normal.z > 0.15f && backwardAlignment >= 0.42f)
            {
                return PalmDirection.Backward;
            }

            var strongest = Mathf.Max(absoluteX, Mathf.Max(absoluteY, absoluteZ));
            if (strongest < minimumAxisAlignment)
            {
                return PalmDirection.Unknown;
            }

            if (absoluteX >= absoluteY)
            {
                return normal.x < 0f ? PalmDirection.Left : PalmDirection.Right;
            }

            return normal.y < 0f ? PalmDirection.Down : PalmDirection.Up;
        }
    }
}

