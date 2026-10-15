using System.Collections.Generic;
using MagicMotion.Config;
using MagicMotion.Core;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Motion
{
    [DisallowMultipleComponent]
    public sealed class MotionAnalyzer : MonoBehaviour
    {
        private readonly Dictionary<Handedness, PushMotionTracker> pushTrackers =
            new Dictionary<Handedness, PushMotionTracker>();
        private readonly Dictionary<Handedness, double> lastSeenTimestamps =
            new Dictionary<Handedness, double>();

        // Fast pushes blur the camera frame and MediaPipe may drop a couple of
        // detections; only reset the tracker after the hand stays missing this
        // long, otherwise every push would wipe its own sample history.
        private const double LossToleranceSeconds = 0.25d;

        private MotionConfig config;

        private void Start()
        {
            config = FindFirstObjectByType<ConfigManager>()?.Motion ?? new MotionConfig();
            pushTrackers[Handedness.Left] = new PushMotionTracker(config);
            pushTrackers[Handedness.Right] = new PushMotionTracker(config);
            lastSeenTimestamps[Handedness.Left] = double.NegativeInfinity;
            lastSeenTimestamps[Handedness.Right] = double.NegativeInfinity;
            GameManager.Instance.Events.Subscribe<HandTrackingResult>(HandleHands);
        }

        private void HandleHands(HandTrackingResult result)
        {
            var sawLeft = false;
            var sawRight = false;

            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                if (hand == null || !hand.IsValid || !pushTrackers.TryGetValue(hand.Handedness, out var tracker))
                {
                    continue;
                }

                sawLeft |= hand.Handedness == Handedness.Left;
                sawRight |= hand.Handedness == Handedness.Right;
                lastSeenTimestamps[hand.Handedness] = result.Timestamp;
                var detection = tracker.Add(MotionSampleFactory.Create(hand, result.Timestamp));
                if (detection.Detected)
                {
                    // Emitted for every accepted push so real-device runs can be
                    // tuned against the actual growth / depth / drift numbers
                    // instead of guessing at thresholds.
                    Debug.Log($"[MagicMotion.Diag] pushmetrics hand={hand.Handedness} " +
                              $"w={detection.PalmWidth:0.000} " +
                              $"gate={(detection.PalmWidth >= config.nearPalmWidth ? "near" : "far")} " +
                              $"growth={detection.ScaleGrowth:0.00} " +
                              $"depth={detection.DepthChange:0.000} " +
                              $"vel={detection.Velocity:0.00} " +
                              $"lateral={detection.LateralDelta:0.00}");
                    GameManager.Instance.Events.Publish(new MotionDetected(
                        MotionType.Push,
                        hand.Handedness,
                        detection.Confidence,
                        detection.Velocity,
                        result.Timestamp));
                }
            }

            if (!sawLeft &&
                result.Timestamp - lastSeenTimestamps[Handedness.Left] > LossToleranceSeconds)
            {
                pushTrackers[Handedness.Left].Reset();
            }

            if (!sawRight &&
                result.Timestamp - lastSeenTimestamps[Handedness.Right] > LossToleranceSeconds)
            {
                pushTrackers[Handedness.Right].Reset();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleHands);
            }

            foreach (var tracker in pushTrackers.Values)
            {
                tracker.Reset();
            }
        }
    }
}

