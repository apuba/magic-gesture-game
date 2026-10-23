using System.Collections.Generic;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Tests
{
    internal static class GestureHandFactory
    {
        public static HandData OpenPalm()
        {
            var points = BasePalm();
            Set(points, 1, -0.35f, 0.25f); Set(points, 2, -0.60f, 0.50f);
            Set(points, 3, -0.85f, 0.75f); Set(points, 4, -1.10f, 1.00f);
            SetStraightFinger(points, 5, -0.45f, 0.75f);
            SetStraightFinger(points, 9, -0.15f, 0.85f);
            SetStraightFinger(points, 13, 0.15f, 0.80f);
            SetStraightFinger(points, 17, 0.45f, 0.68f);
            return Build(points);
        }

        public static HandData OpenPalmWithBentThumb()
        {
            var points = BasePalm();
            Set(points, 1, -0.35f, 0.25f); Set(points, 2, -0.60f, 0.50f);
            Set(points, 3, -0.85f, 0.75f); Set(points, 4, -0.85f, 1.00f);
            SetStraightFinger(points, 5, -0.45f, 0.75f);
            SetStraightFinger(points, 9, -0.15f, 0.85f);
            SetStraightFinger(points, 13, 0.15f, 0.80f);
            SetStraightFinger(points, 17, 0.45f, 0.68f);
            return Build(points);
        }

        public static HandData Fist()
        {
            var points = BasePalm();
            Set(points, 1, -0.30f, 0.25f); Set(points, 2, -0.45f, 0.45f);
            Set(points, 3, -0.20f, 0.55f); Set(points, 4, 0.00f, 0.42f);
            SetFoldedFinger(points, 5, -0.45f, 0.75f);
            SetFoldedFinger(points, 9, -0.15f, 0.85f);
            SetFoldedFinger(points, 13, 0.15f, 0.80f);
            SetFoldedFinger(points, 17, 0.45f, 0.68f);
            return Build(points);
        }

        public static HandData SwordFinger()
        {
            var points = BasePalm();
            Set(points, 1, -0.30f, 0.25f); Set(points, 2, -0.45f, 0.45f);
            Set(points, 3, -0.20f, 0.55f); Set(points, 4, 0.00f, 0.42f);
            SetStraightFinger(points, 5, -0.12f, 0.75f);
            SetStraightFinger(points, 9, 0.02f, 0.85f);
            SetFoldedFinger(points, 13, 0.15f, 0.80f);
            SetFoldedFinger(points, 17, 0.45f, 0.68f);
            return Build(points);
        }

        // The sword finger with the thumb sticking out. The thumb is exactly
        // the joint the tracker guesses when the back of the hand is turned to
        // the camera, so this is the pose that used to fall in and out of the
        // pointing verdict with it.
        public static HandData PointingIndexWithThumbOut()
        {
            var points = BasePalm();
            Set(points, 1, -0.35f, 0.25f); Set(points, 2, -0.60f, 0.50f);
            Set(points, 3, -0.85f, 0.75f); Set(points, 4, -1.10f, 1.00f);
            SetStraightFinger(points, 5, -0.12f, 0.75f);
            SetStraightFinger(points, 9, 0.02f, 0.85f);
            SetFoldedFinger(points, 13, 0.15f, 0.80f);
            SetFoldedFinger(points, 17, 0.45f, 0.68f);
            return Build(points);
        }

        private static List<Vector3> BasePalm()
        {
            var points = new List<Vector3>(new Vector3[HandData.RequiredLandmarkCount]);
            Set(points, 0, 0f, 0f);
            return points;
        }

        private static void SetStraightFinger(List<Vector3> points, int mcp, float x, float y)
        {
            Set(points, mcp, x, y);
            Set(points, mcp + 1, x, y + 0.45f);
            Set(points, mcp + 2, x, y + 0.85f);
            Set(points, mcp + 3, x, y + 1.20f);
        }

        private static void SetFoldedFinger(List<Vector3> points, int mcp, float x, float y)
        {
            Set(points, mcp, x, y);
            Set(points, mcp + 1, x, y + 0.35f);
            Set(points, mcp + 2, x * 0.45f, y + 0.20f);
            Set(points, mcp + 3, x * 0.15f, 0.48f);
        }

        private static void Set(List<Vector3> points, int index, float x, float y)
        {
            points[index] = new Vector3(x, y, 0f);
        }

        private static HandData Build(List<Vector3> points)
        {
            var landmarks = new List<HandLandmark>(points.Count);
            for (var index = 0; index < points.Count; index++)
            {
                landmarks.Add(new HandLandmark(index, points[index]));
            }

            return new HandData(Handedness.Right, 0.95f, Vector3.forward, landmarks);
        }
    }
}

