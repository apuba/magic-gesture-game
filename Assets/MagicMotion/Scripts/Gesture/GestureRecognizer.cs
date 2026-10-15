using MagicMotion.Core;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Gesture
{
    [DisallowMultipleComponent]
    public sealed class GestureRecognizer : MonoBehaviour
    {
        [SerializeField, Min(1)] private int confirmationFrames = 2;
        [SerializeField, Min(1)] private int lossConfirmationFrames = 3;

        private BasicGestureClassifier classifier;
        private GestureStabilizer stabilizer;

        private void Start()
        {
            classifier = new BasicGestureClassifier();
            stabilizer = new GestureStabilizer(confirmationFrames, lossConfirmationFrames);
            GameManager.Instance.Events.Subscribe<HandTrackingResult>(HandleHands);
        }

        private void HandleHands(HandTrackingResult result)
        {
            var sawLeft = false;
            var sawRight = false;

            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                // This score describes Left/Right classification, not gesture
                // quality. Filtering on it discarded otherwise valid landmarks.
                if (hand == null || !hand.IsValid || hand.Handedness == Handedness.Unknown)
                {
                    continue;
                }

                sawLeft |= hand.Handedness == Handedness.Left;
                sawRight |= hand.Handedness == Handedness.Right;
                var classification = classifier.Classify(hand);
                if (classification.Type == GestureType.Unknown)
                {
                    PublishLostIfStable(hand.Handedness, result.Timestamp);
                }
                else if (stabilizer.TryConfirm(hand.Handedness, classification.Type, out var confirmed))
                {
                    GameManager.Instance.Events.Publish(new GestureDetected(
                        confirmed,
                        hand.Handedness,
                        classification.Confidence,
                        result.Timestamp));
                }
            }

            if (!sawLeft)
            {
                PublishLostIfStable(Handedness.Left, result.Timestamp);
            }

            if (!sawRight)
            {
                PublishLostIfStable(Handedness.Right, result.Timestamp);
            }
        }

        private void PublishLostIfStable(Handedness hand, double timestamp)
        {
            if (stabilizer.TryResetAfterMissing(hand, out var previous))
            {
                GameManager.Instance.Events.Publish(new GestureLost(previous, hand, timestamp));
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleHands);
            }

            stabilizer?.ResetAll();
        }
    }
}

