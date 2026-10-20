using System;

namespace MagicMotion.Config
{
    [Serializable]
    public sealed class MotionConfig
    {
        public float minimumDuration = 0.12f;
        // Also the burst window: a push has to reach its growth inside this
        // duration, so slow approaches cannot accumulate enough movement.
        public float maximumDuration = 0.45f;
        public float minimumScaleGrowth = 0.15f;
        // Growth is a relative measure: growth = push / (distance - push), so one
        // fixed gate asks for a larger physical push the farther away the hand
        // is. Palm width is the only distance cue available (MediaPipe's landmark
        // depth is relative, not metres), so palms narrower than nearPalmWidth --
        // i.e. farther hands -- are judged against this lower gate instead.
        // Kept equal to minimumScaleGrowth for now. A lower far gate was tried
        // and reverted: on-device, pushes at ~1m and the interference moves both
        // sat around 0.25-0.44 growth, so the lower gate let interference
        // through and one push tripped the tracker repeatedly.
        public float minimumScaleGrowthFar = 0.40f;
        // Palm width, in normalised image units, at or above which a hand counts
        // as close enough for the stricter minimumScaleGrowth. Measured with
        // replay_runtime.py / calibrate_distance.py.
        public float nearPalmWidth = 0.105f;
        // Below this palm width the hand is too far away for a push to be told
        // apart from ordinary movement, so the HUD asks the user to come closer
        // instead of silently doing nothing. A hint, not a gate: detection is
        // left alone because its own thresholds already reject far interference.
        public float nearEnoughPalmWidth = 0.08f;
        // Hysteresis partner for nearEnoughPalmWidth: once the too-far hint is
        // on, the palm has to grow past this before it clears. Without the gap,
        // per-frame jitter in the estimated width flipped the hint several times
        // a second, which flickered the panel and re-triggered the spoken hint.
        public float farPalmWidthExit = 0.095f;
        public float minimumScaleVelocity = 0.25f;
        public float minimumDepthChange = 0.04f;
        public float maximumLateralDrift = 0.75f;
        public float cooldownSeconds = 0.60f;
        // Share of the predicted perspective drift that is subtracted before the
        // remaining image movement is judged as sideways drift (see
        // PushMotionTracker).
        public float assumedAxialShare = 0.5f;
        // The palm has to shrink back by this share of its width at the previous
        // detection before another push can fire, so one long approach cannot be
        // reported repeatedly.
        public float minimumRetractRatio = 0.03f;
    }
}
