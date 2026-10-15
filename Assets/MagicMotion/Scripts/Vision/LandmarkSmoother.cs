using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMotion.Vision
{
    public sealed class LandmarkSmoother
    {
        private readonly float smoothing;
        private readonly Dictionary<Handedness, Vector3[]> history =
            new Dictionary<Handedness, Vector3[]>();

        public LandmarkSmoother(float smoothing)
        {
            if (smoothing < 0f || smoothing >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(smoothing),
                    "Smoothing must be in the range [0, 1).");
            }

            this.smoothing = smoothing;
        }

        public HandData Process(HandData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                throw new ArgumentException("A valid 21-landmark hand is required.", nameof(hand));
            }

            if (!history.TryGetValue(hand.Handedness, out var previous))
            {
                previous = new Vector3[HandData.RequiredLandmarkCount];
                for (var index = 0; index < previous.Length; index++)
                {
                    previous[index] = hand.Landmarks[index].Position;
                }

                history[hand.Handedness] = previous;
            }

            var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
            var currentWeight = 1f - smoothing;
            for (var index = 0; index < HandData.RequiredLandmarkCount; index++)
            {
                var current = hand.Landmarks[index].Position;
                var filtered = previous[index] * smoothing + current * currentWeight;
                previous[index] = filtered;
                landmarks.Add(new HandLandmark(index, filtered));
            }

            var smoothed = new HandData(
                hand.Handedness,
                hand.Confidence,
                Vector3.zero,
                landmarks);

            return new HandData(
                smoothed.Handedness,
                smoothed.Confidence,
                HandGeometry.CalculatePalmNormal(smoothed),
                smoothed.Landmarks);
        }

        public void Reset(Handedness handedness)
        {
            history.Remove(handedness);
        }

        public void ResetAll()
        {
            history.Clear();
        }
    }
}

