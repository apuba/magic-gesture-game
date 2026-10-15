using System.Collections.Generic;
using MagicMotion.Config;
using UnityEngine;

namespace MagicMotion.Motion
{
    public sealed class PushMotionTracker
    {
        private const float ReferenceScaleGrowth = 0.5f;
        private const float ReferenceScaleVelocity = 0.8f;
        private const float ReferenceDepthChange = 0.2f;

        private readonly MotionConfig config;
        private readonly Queue<HandMotionSample> samples = new Queue<HandMotionSample>();
        private double lastDetectionTime = double.NegativeInfinity;
        private double lastSampleTime = double.NegativeInfinity;
        private float lastDetectionWidth = float.MaxValue;
        private bool rearmed = true;

        public PushMotionTracker(MotionConfig config)
        {
            this.config = config ?? new MotionConfig();
            ValidateConfig(this.config);
        }

        public PushDetection Add(HandMotionSample sample)
        {
            if (sample.Timestamp < lastSampleTime)
            {
                Reset();
            }

            lastSampleTime = sample.Timestamp;
            samples.Enqueue(sample);
            TrimOldSamples(sample.Timestamp);

            // One continuous approach must only ever report a single push: after a
            // detection the palm has to withdraw again, otherwise every extra
            // frame of the same movement would queue up another cast.
            if (!rearmed &&
                sample.PalmWidth <= lastDetectionWidth * (1f - config.minimumRetractRatio))
            {
                rearmed = true;
            }

            if (!rearmed ||
                sample.Timestamp - lastDetectionTime < config.cooldownSeconds ||
                samples.Count < 2)
            {
                return default;
            }

            // Pair the newest sample with the historical start that maximises
            // palm-width growth inside the duration window. Pairing against the
            // queue head would dilute the measurement whenever the hand idled
            // before the push, because an old baseline shrinks the growth.
            var bestGrowth = 0f;
            var bestDuration = 0d;
            var bestFirst = default(HandMotionSample);
            var found = false;
            foreach (var candidate in samples)
            {
                var duration = sample.Timestamp - candidate.Timestamp;
                if (duration < config.minimumDuration || duration > config.maximumDuration)
                {
                    continue;
                }

                var candidateWidth = Mathf.Max(candidate.PalmWidth, 0.0001f);
                var growth = (sample.PalmWidth - candidate.PalmWidth) / candidateWidth;
                if (!found || growth > bestGrowth)
                {
                    found = true;
                    bestGrowth = growth;
                    bestDuration = duration;
                    bestFirst = candidate;
                }
            }

            if (!found)
            {
                return default;
            }

            var initialWidth = Mathf.Max(bestFirst.PalmWidth, 0.0001f);
            var scaleGrowth = bestGrowth;
            var scaleVelocity = bestGrowth / (float)bestDuration;
            var depthChange = (bestFirst.MeanDepth - sample.MeanDepth) / initialWidth;
            // A hand approaching the lens drifts outward in the image under
            // perspective, along the ray from the image centre to the palm. Two
            // limits bound that drift exactly: pushing straight at the lens does not
            // move the image position at all, while pushing parallel to the optical
            // axis moves the palm outward by precisely (offset x growth). Real
            // pushes sit somewhere between the two, so subtract the midpoint of the
            // bounds and judge whatever image movement is left. Ignoring the whole
            // outward component instead (the previous approach) leaves any outward
            // motion free, which is what turned slow hand waves and outward flings
            // into casts; the residual is small for every genuine push because the
            // two bounds differ by at most (offset x growth).
            var radialOffset = new Vector2(
                bestFirst.PalmCenter.x - 0.5f,
                bestFirst.PalmCenter.y - 0.5f);
            var travelled = new Vector2(
                sample.PalmCenter.x - bestFirst.PalmCenter.x,
                sample.PalmCenter.y - bestFirst.PalmCenter.y);
            var expectedDrift = radialOffset * (bestGrowth * config.assumedAxialShare);
            var lateralDelta = (travelled - expectedDrift).magnitude / initialWidth;

            // A forward push is the palm growing as it approaches the lens, so
            // scale growth is the only signal allowed to open the gate. Landmark
            // depth used to qualify as an alternative, but on-device it swings
            // wildly -- genuine pushes routinely report a negative change -- and
            // it let slow horizontal waves through on noise alone. It now only
            // contributes to the confidence below. Lateral drift is still
            // rejected so pure side movement is not mistaken for a push.
            // Widened for far hands. Measured on recorded pushes with
            // replay_runtime.py: this lifts recall from 63% to 80% without adding
            // a real false positive, because the pushes it recovers are genuine
            // ones that were only small in relative terms. Dropping the far gate
            // below 0.22 starts admitting stationary hands whose estimated palm
            // width jitters upward without the hand ever moving.
            var growthGate = initialWidth >= config.nearPalmWidth
                ? config.minimumScaleGrowth
                : config.minimumScaleGrowthFar;
            var pushing = scaleGrowth >= growthGate;
            if (!pushing ||
                scaleVelocity < config.minimumScaleVelocity ||
                lateralDelta > config.maximumLateralDrift)
            {
                return default;
            }

            // Scored against fixed reference points instead of the gate
            // thresholds. Tying confidence to the thresholds made every tuning
            // change silently rescale it: raising minimumScaleGrowth shrank the
            // denominator's effect and pushed genuine pushes below the skill
            // layer's confidence gate, so they were detected and then dropped.
            // These references describe a decisive push (palm width up by half,
            // at roughly one width per second) and never move with the gates.
            var growthScore = Mathf.Clamp01(scaleGrowth / ReferenceScaleGrowth);
            var velocityScore = Mathf.Clamp01(scaleVelocity / ReferenceScaleVelocity);
            var depthScore = Mathf.Clamp01(depthChange / ReferenceDepthChange);
            // Scale growth drives the confidence; landmark depth is too noisy to
            // outrank it and is only allowed a partial say.
            var approachScore = Mathf.Max(growthScore, depthScore * 0.5f);
            var confidence = (approachScore + velocityScore) * 0.5f;

            lastDetectionTime = sample.Timestamp;
            lastDetectionWidth = sample.PalmWidth;
            rearmed = false;
            samples.Clear();
            samples.Enqueue(sample);
            return new PushDetection(true, confidence, scaleVelocity, scaleGrowth, depthChange, lateralDelta, initialWidth);
        }

        public void Reset()
        {
            samples.Clear();
            lastDetectionTime = double.NegativeInfinity;
            lastSampleTime = double.NegativeInfinity;
            lastDetectionWidth = float.MaxValue;
            rearmed = true;
        }

        private void TrimOldSamples(double currentTimestamp)
        {
            while (samples.Count > 0 &&
                   currentTimestamp - samples.Peek().Timestamp > config.maximumDuration)
            {
                samples.Dequeue();
            }
        }

        private static void ValidateConfig(MotionConfig value)
        {
            if (value.minimumDuration <= 0f ||
                value.maximumDuration <= value.minimumDuration ||
                value.minimumScaleGrowth <= 0f ||
                value.minimumScaleGrowthFar <= 0f ||
                value.nearPalmWidth <= 0f ||
                value.minimumScaleVelocity <= 0f ||
                value.minimumDepthChange <= 0f ||
                value.maximumLateralDrift <= 0f ||
                value.cooldownSeconds < 0f ||
                value.assumedAxialShare < 0f ||
                value.assumedAxialShare > 1f ||
                value.minimumRetractRatio < 0f ||
                value.minimumRetractRatio >= 1f)
            {
                throw new System.ArgumentException("Motion configuration contains invalid thresholds.");
            }
        }
    }
}

