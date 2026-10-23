using MagicMotion.Camera;
using MagicMotion.Core;
using MagicMotion.Motion;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Gesture
{
    /// <summary>
    /// Drives one casting attempt end to end: the sword-finger start and the
    /// ordered turn summon the base rune, a direction picks the element, the
    /// rune then waits on the hand, and a real push releases it.
    /// </summary>
    /// <remarks>
    /// Recognition coordinates and display coordinates are separate things and
    /// stay separate here. The direction is measured at the palm, which is
    /// stable under finger noise, while the rune is drawn from the fingertips,
    /// which is where the player expects to see it. Drawing belongs to the
    /// view; this component only exposes the hand it settled on and the stage
    /// the attempt is in.
    ///
    /// The active hand is locked once the turn completes, so a second hand
    /// entering the frame cannot take the cast over halfway through.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RuneCastController : MonoBehaviour
    {
        private const float LostResultSeconds = 0.35f;
        private const float DirectionDominance = 1.25f;

        // Waiting times sit at the relaxed end of the design document's
        // suggested ranges: they are the starting point for device calibration,
        // not a measured baseline.
        [SerializeField] private double armedTimeoutSeconds = 10d;
        [SerializeField] private double releaseWindowSeconds = 10d;
        [SerializeField] private double castCooldownSeconds = 0.6d;
        [SerializeField] private float directionTravelFraction = 0.12f;
        [SerializeField] private double directionDeadlineSeconds = 10.0d;
        [SerializeField] private double directionConfirmationSeconds = 0.12d;

        private CameraManager cameraManager;
        private SwordFingerDetector swordFingerDetector;
        private HandFacingDetector facingDetector;
        private BasicGestureClassifier palmGestureClassifier;
        private FlipSequenceRecognizer summonSequence;
        private DirectionSelectionRecognizer directionSelection;
        private RuneCastStateMachine castState;

        private HandData latestHand;
        private float latestWidthToHeight = 16f / 9f;
        private bool hasLatestHand;
        private Handedness activeHand = Handedness.Unknown;
        private double lastResultAt = double.NegativeInfinity;

        public RuneCastStage Stage =>
            castState == null ? RuneCastStage.Idle : castState.Stage;

        public RuneElement Element =>
            directionSelection == null ? RuneElement.None : directionSelection.Element;

        public RuneElement SelectionCandidate =>
            directionSelection == null ? RuneElement.None : directionSelection.Candidate;

        public Vector2 SelectionTravel =>
            directionSelection == null ? Vector2.zero : directionSelection.Travel;

        /// <summary>
        /// True while a rune is on screen, which is what tells the palm strike to
        /// stand down so one push never launches two spells.
        /// </summary>
        public bool HasPendingRune =>
            Stage == RuneCastStage.BaseRuneReady ||
            Stage == RuneCastStage.RuneReady ||
            Stage == RuneCastStage.Releasing;

        public bool ShouldShowRune => HasPendingRune && HasActiveHand;

        public bool HasActiveHand => hasLatestHand && latestHand != null;
        public HandData ActiveHand => latestHand;

        /// <summary>
        /// Shape of the frame the last result arrived on, so the view can map
        /// landmarks to screen coordinates the same way this component did.
        /// </summary>
        public float WidthToHeight => latestWidthToHeight;

        public int ReceivedResultCount { get; private set; }
        public int ValidHandsInLatestResult { get; private set; }

        private void Start()
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
            swordFingerDetector = new SwordFingerDetector();
            facingDetector = new HandFacingDetector();
            palmGestureClassifier = new BasicGestureClassifier();
            summonSequence = new FlipSequenceRecognizer();
            directionSelection = new DirectionSelectionRecognizer(
                directionDeadlineSeconds,
                directionTravelFraction,
                DirectionDominance,
                directionConfirmationSeconds);
            castState = new RuneCastStateMachine(
                armedTimeoutSeconds, releaseWindowSeconds, castCooldownSeconds);

            GameManager.Instance?.Events.Subscribe<HandTrackingResult>(HandleResult);
            GameManager.Instance?.Events.Subscribe<MotionDetected>(HandleMotion);
        }

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (!HasActiveHand || now - lastResultAt > LostResultSeconds)
            {
                ResetCast();
                return;
            }

            var widthToHeight = latestWidthToHeight;

            // The pose verdict ignores the thumb on purpose: with the back of
            // the hand, which is where this sequence starts, turned to the lens
            // the thumb is hidden and gets inferred as straight on some frames,
            // which used to drop the verdict in and out. Facing carries the
            // constraint instead.
            var swordFinger = swordFingerDetector.IsRuneFinger(latestHand, widthToHeight);
            var facing = facingDetector.Detect(latestHand);
            var openPalm =
                palmGestureClassifier.Classify(latestHand, widthToHeight).Type ==
                GestureType.OpenPalm;

            // Once armed the hand is expected to leave the finger sign, so the
            // turn is held open across that transition instead of being
            // cancelled by it.
            summonSequence.Hold(Stage == RuneCastStage.RuneReady ||
                                Stage == RuneCastStage.Releasing);
            summonSequence.Update(swordFinger, facing, now);

            var palmPosition = ToShortSideUnits(
                HandTrackingFrameTransform.LandmarkToGuiNormalized(
                    HandGeometry.CalculatePalmCenter(latestHand),
                    CurrentRotation(),
                    cameraManager != null && cameraManager.IsFrontFacing,
                    CurrentVerticallyMirrored()));

            var previous = Stage;
            castState.Update(
                summonSequence.Stage,
                openPalm,
                directionSelection.Locked,
                now,
                swordFinger);

            // The anchor is the palm position at the moment the rune appears;
            // everything after that is measured against it.
            if (previous != RuneCastStage.BaseRuneReady && Stage == RuneCastStage.BaseRuneReady)
            {
                directionSelection.Begin(palmPosition, now);
            }
            else if (Stage == RuneCastStage.BaseRuneReady)
            {
                directionSelection.Update(palmPosition, now);
            }

            if (previous != RuneCastStage.Idle && Stage == RuneCastStage.Idle)
            {
                ClearAttempt();
            }

            if (directionSelection.Expired)
            {
                ResetCast();
            }
        }

        private void HandleResult(HandTrackingResult result)
        {
            ReceivedResultCount++;
            ValidHandsInLatestResult = 0;
            lastResultAt = Time.realtimeSinceStartupAsDouble;
            latestWidthToHeight = result.WidthToHeight;

            HandData best = null;
            HandData locked = null;
            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                if (hand == null || !hand.IsValid)
            {
                    continue;
                }

                ValidHandsInLatestResult++;
                if (activeHand != Handedness.Unknown && hand.Handedness == activeHand)
                {
                    locked = hand;
                }

                if (best == null || hand.Confidence > best.Confidence)
                {
                    best = hand;
                }
            }

            // A hand that is already mid-cast keeps it; only an idle cast picks
            // up whichever hand is tracked most confidently.
            var selected = locked ?? best;
            if (selected == null)
            {
                ClearAttempt();
                return;
            }

            if (activeHand == Handedness.Unknown)
            {
                activeHand = selected.Handedness;
            }

            latestHand = selected;
            hasLatestHand = true;
        }

        private void HandleMotion(MotionDetected message)
        {
            if (castState == null || !castState.IsArmed)
            {
                return;
            }

            if (message.Motion != MotionType.Push)
            {
                return;
            }

            if (activeHand != Handedness.Unknown && message.Hand != activeHand)
            {
                return;
            }

            GameManager.Instance?.Events.Publish(
                new RuneReleased(directionSelection.Element, message.Hand, message.Timestamp));
            castState.Release(message.Timestamp);
            ClearAttempt();
        }

        private int CurrentRotation()
        {
            var texture = cameraManager == null ? null : cameraManager.Texture;
            return HandTrackingFrameTransform.NormalizeRotation(
                texture == null ? 0 : texture.videoRotationAngle);
        }

        private bool CurrentVerticallyMirrored()
        {
            var texture = cameraManager == null ? null : cameraManager.Texture;
            return texture != null && texture.videoVerticallyMirrored;
        }

        // A fraction of the frame means different things on a 16:9 phone and a
        // portrait one, so distances are measured in units of the shorter side:
        // the same hand travel then covers the same number of units whichever
        // way round the player is holding the device.
        private static Vector2 ToShortSideUnits(Vector2 guiNormalized)
        {
            var shortSide = Mathf.Max(Mathf.Min(Screen.width, Screen.height), 1);
            return new Vector2(
                guiNormalized.x * Screen.width / shortSide,
                guiNormalized.y * Screen.height / shortSide);
        }

        private void ResetCast()
        {
            ClearAttempt();
            castState?.Reset();
        }

        // Drops the attempt but keeps the stage machine where it is, which is
        // what a release needs: the stage machine has just entered cooldown and
        // must be left alone to run it out.
        private void ClearAttempt()
        {
            summonSequence?.Reset();
            directionSelection?.Reset();
            activeHand = Handedness.Unknown;
            latestHand = null;
            hasLatestHand = false;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleResult);
            GameManager.Instance.Events.Unsubscribe<MotionDetected>(HandleMotion);
        }
    }
}
