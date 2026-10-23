using System;
using System.Collections.Generic;
using MagicMotion.Config;
using MagicMotion.Core;
using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Skill
{
    [DisallowMultipleComponent]
    public sealed class SkillManager : MonoBehaviour
    {
        private sealed class HandContext
        {
            public GestureType Gesture;
            public float GestureConfidence;
            public PalmDirection Palm;
            public double PalmTimestamp;
            public double LastForwardTimestamp = double.NegativeInfinity;
            public double LastOpenPalmTime = double.NegativeInfinity;
            public float LastOpenPalmConfidence;
        }

        // The palm normal wobbles while the hand is moving fast, so a cast is
        // accepted when the palm faced the camera within this window instead of
        // requiring it on the exact frame the push fires. Kept short: a longer
        // window let hand waves and palm rotations qualify long after the palm
        // had actually turned away from the camera.
        private const double PalmMaxAge = 0.2d;

        // A push is a fast motion; the open-palm gesture often drops a frame or
        // two while the hand accelerates, so we accept a cast when the hand was
        // open within this window rather than requiring OpenPalm on the exact
        // frame the push fires. Long enough to cover a couple of dropped frames,
        // short enough that unrelated waving cannot keep the requirement
        // satisfied for a second or more.
        private const double GestureMemorySeconds = 0.35d;

        private readonly List<SkillDefinition> definitions = new List<SkillDefinition>();
        private readonly Dictionary<Handedness, HandContext> handContexts =
            new Dictionary<Handedness, HandContext>();
        private readonly Dictionary<string, SkillRuntimeState> runtimeStates =
            new Dictionary<string, SkillRuntimeState>();

        private PalmDirectionClassifier palmClassifier;
        private RuneCastController runeCastController;
        private static readonly Handedness[] TrackedHands = { Handedness.Left, Handedness.Right };

        private void Start()
        {
            palmClassifier = new PalmDirectionClassifier();
            handContexts[Handedness.Left] = new HandContext();
            handContexts[Handedness.Right] = new HandContext();

            runeCastController = FindFirstObjectByType<RuneCastController>();

            var catalog = FindFirstObjectByType<ConfigManager>()?.Skills;
            if (catalog?.skills == null)
            {
                throw new InvalidOperationException("Skill configuration has not been loaded.");
            }

            var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < catalog.skills.Length; index++)
            {
                var definition = SkillDefinition.FromConfig(catalog.skills[index]);
                if (!loadedIds.Add(definition.Id))
                {
                    throw new InvalidOperationException($"Duplicate skill id: {definition.Id}");
                }

                definitions.Add(definition);
                runtimeStates[RuntimeKey(definition.Id, Handedness.Left)] = new SkillRuntimeState(definition);
                runtimeStates[RuntimeKey(definition.Id, Handedness.Right)] = new SkillRuntimeState(definition);
            }

            GameManager.Instance.Events.Subscribe<GestureDetected>(HandleGestureDetected);
            GameManager.Instance.Events.Subscribe<GestureLost>(HandleGestureLost);
            GameManager.Instance.Events.Subscribe<HandTrackingResult>(HandleHands);
            GameManager.Instance.Events.Subscribe<MotionDetected>(HandleMotionDetected);
        }

        private void Update()
        {
            var timestamp = Time.realtimeSinceStartupAsDouble;
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var definition = definitions[definitionIndex];
                for (var handIndex = 0; handIndex < TrackedHands.Length; handIndex++)
                {
                    var hand = TrackedHands[handIndex];
                    var state = runtimeStates[RuntimeKey(definition.Id, hand)];
                    if (state.Refresh(timestamp))
                    {
                        PublishState(definition, hand, SkillState.Idle, timestamp);
                    }
                }
            }
        }

        private void HandleGestureDetected(GestureDetected message)
        {
            if (!handContexts.TryGetValue(message.Hand, out var context))
            {
                return;
            }

            context.Gesture = message.Gesture;
            context.GestureConfidence = message.Confidence;
            if (message.Gesture == GestureType.OpenPalm)
            {
                context.LastOpenPalmTime = message.Timestamp;
                context.LastOpenPalmConfidence = message.Confidence;
            }
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var state = runtimeStates[RuntimeKey(definition.Id, message.Hand)];
                if (definition.Gesture == message.Gesture && state.TryPrepare(message.Timestamp))
                {
                    PublishState(definition, message.Hand, SkillState.Prepare, message.Timestamp);
                }
            }
        }

        private void HandleGestureLost(GestureLost message)
        {
            if (!handContexts.TryGetValue(message.Hand, out var context))
            {
                return;
            }

            context.Gesture = GestureType.Unknown;
            context.GestureConfidence = 0f;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var state = runtimeStates[RuntimeKey(definition.Id, message.Hand)];
                var previous = state.State;
                state.Cancel();
                if (previous != state.State)
                {
                    PublishState(definition, message.Hand, state.State, message.Timestamp);
                }
            }
        }

        private void HandleHands(HandTrackingResult result)
        {
            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                if (hand == null || !handContexts.TryGetValue(hand.Handedness, out var context))
                {
                    continue;
                }

                context.Palm = palmClassifier.Classify(hand);
                context.PalmTimestamp = result.Timestamp;
                if (context.Palm == PalmDirection.Forward)
                {
                    context.LastForwardTimestamp = result.Timestamp;
                }
            }
        }

        private void HandleMotionDetected(MotionDetected message)
        {
            if (!handContexts.TryGetValue(message.Hand, out var context))
            {
                return;
            }

            // The palm currently points away from the camera: nothing that
            // happened a moment ago should be able to turn that posture into a
            // cast. Without this guard a rotating hand satisfied the remembered
            // "palm forward" requirement while its back was already to the lens.
            if (context.Palm == PalmDirection.Backward)
            {
                return;
            }

            // A rune is waiting to be released, so this push belongs to the spell
            // and not to the bare palm strike. The event bus hands the message to
            // every subscriber, so the only place the strike can be held back is
            // here, before it is matched.
            if (runeCastController != null && runeCastController.HasPendingRune)
            {
                return;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var palmFresh = message.Timestamp - context.LastForwardTimestamp <= PalmMaxAge;
                // Clamp so a palm observation slightly older than inputMaxAge
                // still passes the matcher's freshness check; PalmMaxAge is the
                // intended tolerance window.
                var palmTimestamp = palmFresh
                    ? System.Math.Max(
                        context.LastForwardTimestamp,
                        message.Timestamp - definition.InputMaxAge)
                    : context.PalmTimestamp;

                // A push is a fast motion and the open-palm gesture frequently
                // drops a frame or two as the hand accelerates, so accept a cast
                // when the hand was open within a short memory window rather than
                // requiring OpenPalm on the exact frame the push fires.
                var rememberedOpenPalm =
                    message.Timestamp - context.LastOpenPalmTime <= GestureMemorySeconds;
                var effectiveGesture = (context.Gesture == GestureType.OpenPalm || rememberedOpenPalm)
                    ? GestureType.OpenPalm
                    : context.Gesture;
                // Reuse the confidence that was actually observed for the open
                // palm. Inventing a floor here would let the skill's own
                // confidence threshold be bypassed.
                var effectiveConfidence = effectiveGesture == GestureType.OpenPalm
                    ? (context.Gesture == GestureType.OpenPalm
                        ? context.GestureConfidence
                        : context.LastOpenPalmConfidence)
                    : context.GestureConfidence;

                var input = new SkillInputSnapshot(
                    message.Hand,
                    effectiveGesture,
                    effectiveConfidence,
                    palmFresh ? PalmDirection.Forward : context.Palm,
                    palmTimestamp,
                    message.Motion,
                    message.Confidence,
                    message.Timestamp);

                var state = runtimeStates[RuntimeKey(definition.Id, message.Hand)];
                state.Refresh(message.Timestamp);
                if (!SkillInputMatcher.Matches(definition, input) || !state.TryCast(message.Timestamp))
                {
                    continue;
                }

                PublishState(definition, message.Hand, SkillState.Cast, message.Timestamp);
                GameManager.Instance.Events.Publish(new SkillCast(
                    definition.Id,
                    definition.Name,
                    definition.Vfx,
                    message.Hand,
                    message.Timestamp));
                state.EnterCooldown(message.Timestamp);
                PublishState(definition, message.Hand, SkillState.Cooldown, message.Timestamp);
            }
        }

        private static string RuntimeKey(string skillId, Handedness hand)
        {
            return $"{skillId}:{hand}";
        }

        private static void PublishState(
            SkillDefinition definition,
            Handedness hand,
            SkillState state,
            double timestamp)
        {
            GameManager.Instance.Events.Publish(new SkillStateChanged(definition.Id, hand, state, timestamp));
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.Events.Unsubscribe<GestureDetected>(HandleGestureDetected);
            GameManager.Instance.Events.Unsubscribe<GestureLost>(HandleGestureLost);
            GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleHands);
            GameManager.Instance.Events.Unsubscribe<MotionDetected>(HandleMotionDetected);
        }
    }
}

