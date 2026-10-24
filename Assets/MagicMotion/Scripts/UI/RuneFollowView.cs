using MagicMotion.Camera;
using MagicMotion.Core;
using MagicMotion.Gesture;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.UI
{
    /// <summary>
    /// Draws the talisman at the point the cast controller settled on. It
    /// decides nothing about gestures or stages: it only smooths the motion and
    /// picks the artwork for the element the controller reports.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneFollowView : MonoBehaviour
    {
        public const string BuildMarker = "RF-CHECK-B31";

        private const float FollowSeconds = 0.035f;
        private const float LostResultSeconds = 0.35f;
        private const float FireRuneLiftSp = 20f;
        private const float FireBurnSeconds = 1.5f;
        private const float FireFinalHeightScale = 1.55f;
        private const float FireInitialGrowth = 0.22f;
        private const float FireTrailSeconds = 0.36f;
        private const int FireTrailCapacity = 10;

        // Blank paper talisman, plus the four elemental variants. They live in
        // Resources so a scene that never wired the fields still resolves them.
        private const string BlankRunePath = "Runes/Rune_Blank";
        private const string ThunderRunePath = "Runes/Rune_Thunder";
        private const string FireRunePath = "Runes/Rune_Fire";
        private const string WaterRunePath = "Runes/Rune_Water";
        private const string WindRunePath = "Runes/Rune_Wind";
        private const string ArrayThunderRunePath = "Runes/Rune_Thunder_Array";
        private const string ArrayFireRunePath = "Runes/Rune_Fire_Array";
        private const string ArrayWaterRunePath = "Runes/Rune_Water_Array";
        private const string ArrayWindRunePath = "Runes/Rune_Wind_Array";
        private const string FireCoreVfxPath = "VFX/Fire/Fire_Core_Sheet";
        private const string FireOuterVfxPath = "VFX/Fire/Fire_Outer_Sheet";
        private const string FireEmberVfxPath = "VFX/Fire/Fire_Ember_Atlas";
        private const string FireGlowVfxPath = "VFX/Fire/Fire_Glow";
        private const string FireTongueVfxPath = "VFX/Fire/Fire_Tongue_Atlas";
        private const string ArrayInnerVfxPath = "VFX/Array/Array_Inner_Bagua";
        private const string ArraySummonVfxPath = "VFX/Array/Array_Summon_Energy";
        private const string ThunderArcVfxPath = "VFX/Thunder_Arc_Sheet";
        private const string WaterVfxPath = "VFX/Water_Vortex_Sheet";
        private const string WindVfxPath = "VFX/Wind_Blade_Sheet";

        [SerializeField] private Texture2D blankRuneTexture;
        [SerializeField] private Texture2D thunderRuneTexture;
        [SerializeField] private Texture2D fireRuneTexture;
        [SerializeField] private Texture2D waterRuneTexture;
        [SerializeField] private Texture2D windRuneTexture;
        [SerializeField] private Texture2D arrayThunderRuneTexture;
        [SerializeField] private Texture2D arrayFireRuneTexture;
        [SerializeField] private Texture2D arrayWaterRuneTexture;
        [SerializeField] private Texture2D arrayWindRuneTexture;
        [SerializeField] private Texture2D fireCoreVfxTexture;
        [SerializeField] private Texture2D fireOuterVfxTexture;
        [SerializeField] private Texture2D fireEmberVfxTexture;
        [SerializeField] private Texture2D fireGlowVfxTexture;
        [SerializeField] private Texture2D fireTongueVfxTexture;
        [SerializeField] private Texture2D arrayInnerVfxTexture;
        [SerializeField] private Texture2D arraySummonVfxTexture;
        [SerializeField] private Texture2D thunderArcVfxTexture;
        [SerializeField] private Texture2D waterVfxTexture;
        [SerializeField] private Texture2D windVfxTexture;

        // The talisman is measured against the hand, not the screen: its height
        // is the fingertip-to-palm span, and its width follows the artwork's
        // proportions. Height scale is the trim to tune on device, and depth
        // weight decides how much of the hand's length along z counts.
        [SerializeField] private float runeHeightScale = 1.5f;
        [SerializeField] private float runeDepthWeight = 0.5f;

        // Rails rather than the intended range: they only catch a hand right
        // against the lens or lost in the distance, where a proportional size
        // would otherwise cover the screen or vanish.
        [SerializeField] private float minimumRuneHeightPixels = 36f;
        [SerializeField] private float maximumRuneHeightPixels = 420f;
        [SerializeField] private float sizeFollowSeconds = 0.09f;
        [SerializeField] private float heldRuneScale = 0.8f;
        [SerializeField] private float depthScaleSensitivity = 3f;
        [SerializeField] private float minimumDepthScale = 0.65f;
        [SerializeField] private float maximumDepthScale = 1.6f;

        private CameraManager cameraManager;
        private RuneCastController castController;
        private RuneFollowTracker tracker;
        private Vector3 renderedTarget;
        private bool hasRenderedTarget;
        private float renderedSpan = 0.2f;
        private bool hasRenderedSpan;
        private Vector3 selectionAnchor;
        private bool hasSelectionAnchor;
        private float selectionArrayHeightPixels;
        private float referenceFingerDepth;
        private float renderedDepthScale = 1f;
        private bool hasReferenceFingerDepth;
        private RuneElement renderedElement = RuneElement.None;
        private float elementTransitionSeconds = 1f;
        private const float ElementTransitionDuration = 0.72f;
        private const float ArrayAppearanceDuration = 0.4f;
        private const float ArrayDismissDuration = 0.4f;
        private RuneCastStage renderedStage = RuneCastStage.Idle;
        private RuneAudioFeedback audioFeedback;
        private float arrayAppearanceSeconds;
        private float arrayDismissSeconds;
        private bool arrayDismissing;
        private Vector2 lastArrayCenter;
        private float lastArrayHeight;
        private double lastResultAt = double.NegativeInfinity;
        private float lastWidthToHeight = 16f / 9f;
        private DeviceOrientation effectiveDeviceOrientation = DeviceOrientation.Portrait;
        private DeviceOrientation candidateDeviceOrientation = DeviceOrientation.Portrait;
        private Vector3 filteredGravity = Vector3.down;
        private float orientationCandidateSince;
        private const float OrientationHoldSeconds = 0.12f;
        private readonly Vector2[] fireTrailPositions = new Vector2[FireTrailCapacity];
        private readonly float[] fireTrailTimes = new float[FireTrailCapacity];
        private int fireTrailCount;
        private int fireTrailWriteIndex;
        private float lastFireTrailSampleAt = float.NegativeInfinity;
        private Vector2 lastFireTrailPosition;

        public bool IsRuneVisible => tracker != null && tracker.Visible;
        public bool HasRenderAnchor => hasRenderedTarget;
        public RuneCastStage CastStage =>
            castController == null ? RuneCastStage.Idle : castController.Stage;
        public RuneElement Element =>
            castController == null ? RuneElement.None : castController.Element;

        // Diagnostic only: why OnGUI last returned early, or where the rune was
        // drawn. Surfaced next to the build marker so a "no rune" report can be
        // triaged from a single screenshot.
        public string DrawState { get; private set; } = "pending";

        private void Start()
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
            castController = FindFirstObjectByType<RuneCastController>();
            audioFeedback = GetComponent<RuneAudioFeedback>() ?? gameObject.AddComponent<RuneAudioFeedback>();

            // Left unassigned in a scene the rune still draws: a missing asset is
            // reported through DrawState rather than failing silently.
            if (blankRuneTexture == null)
            {
                blankRuneTexture = Resources.Load<Texture2D>(BlankRunePath);
            }

            if (thunderRuneTexture == null)
            {
                thunderRuneTexture = Resources.Load<Texture2D>(ThunderRunePath);
            }

            if (fireRuneTexture == null)
            {
                fireRuneTexture = Resources.Load<Texture2D>(FireRunePath);
            }

            if (waterRuneTexture == null)
            {
                waterRuneTexture = Resources.Load<Texture2D>(WaterRunePath);
            }

            if (windRuneTexture == null)
            {
                windRuneTexture = Resources.Load<Texture2D>(WindRunePath);
            }

            if (arrayThunderRuneTexture == null)
            {
                arrayThunderRuneTexture = Resources.Load<Texture2D>(ArrayThunderRunePath);
            }

            if (arrayFireRuneTexture == null)
            {
                arrayFireRuneTexture = Resources.Load<Texture2D>(ArrayFireRunePath);
            }

            if (arrayWaterRuneTexture == null)
            {
                arrayWaterRuneTexture = Resources.Load<Texture2D>(ArrayWaterRunePath);
            }

            if (arrayWindRuneTexture == null)
            {
                arrayWindRuneTexture = Resources.Load<Texture2D>(ArrayWindRunePath);
            }

            fireCoreVfxTexture = fireCoreVfxTexture ?? Resources.Load<Texture2D>(FireCoreVfxPath);
            fireOuterVfxTexture = fireOuterVfxTexture ?? Resources.Load<Texture2D>(FireOuterVfxPath);
            fireEmberVfxTexture = fireEmberVfxTexture ?? Resources.Load<Texture2D>(FireEmberVfxPath);
            fireGlowVfxTexture = fireGlowVfxTexture ?? Resources.Load<Texture2D>(FireGlowVfxPath);
            fireTongueVfxTexture = fireTongueVfxTexture ?? Resources.Load<Texture2D>(FireTongueVfxPath);
            arrayInnerVfxTexture = arrayInnerVfxTexture ?? Resources.Load<Texture2D>(ArrayInnerVfxPath);
            arraySummonVfxTexture = arraySummonVfxTexture ?? Resources.Load<Texture2D>(ArraySummonVfxPath);
            thunderArcVfxTexture = thunderArcVfxTexture ?? Resources.Load<Texture2D>(ThunderArcVfxPath);
            waterVfxTexture = waterVfxTexture ?? Resources.Load<Texture2D>(WaterVfxPath);
            windVfxTexture = windVfxTexture ?? Resources.Load<Texture2D>(WindVfxPath);

            // The summon sequence already proves the pose was held, so the
            // visual does not need its own confirmation delay; the grace on
            // the way out is what keeps it from flickering on a dropped frame.
            tracker = new RuneFollowTracker(0d, 0.18d);

            GameManager.Instance?.Events.Subscribe<HandTrackingResult>(HandleResult);
        }

        private void HandleResult(HandTrackingResult result)
        {
            lastResultAt = Time.realtimeSinceStartupAsDouble;
            lastWidthToHeight = result.WidthToHeight;
        }

        private void Update()
        {
            UpdateDeviceOrientation();
            if (tracker == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            if (now - lastResultAt > LostResultSeconds)
            {
                tracker.Reset();
                hasRenderedTarget = false;
                hasRenderedSpan = false;
                hasSelectionAnchor = false;
                selectionArrayHeightPixels = 0f;
                hasReferenceFingerDepth = false;
                renderedDepthScale = 1f;
                return;
            }

            var showRune = castController != null &&
                           castController.ShouldShowRune &&
                           castController.HasActiveHand;

            var element = castController.Element;
            if (element != renderedElement)
            {
                if (castController.Stage != RuneCastStage.BaseRuneReady && element != RuneElement.None)
                {
                    audioFeedback?.PlayElement(element);
                }
                renderedElement = element;
                elementTransitionSeconds = 0f;
                ResetFireTrail();
            }

            elementTransitionSeconds += Time.unscaledDeltaTime;

            var currentStage = castController.Stage;
            if (currentStage != renderedStage)
            {
                if (currentStage == RuneCastStage.BaseRuneReady)
                {
                    audioFeedback?.PlaySummon();
                }
                if (renderedStage == RuneCastStage.BaseRuneReady &&
                    currentStage != RuneCastStage.BaseRuneReady)
                {
                    audioFeedback?.PlayConfirm();
                    arrayDismissing = true;
                    arrayDismissSeconds = 0f;
                }
                renderedStage = currentStage;
                arrayAppearanceSeconds = currentStage == RuneCastStage.BaseRuneReady
                    ? 0f
                    : arrayAppearanceSeconds;
            }

            if (currentStage == RuneCastStage.BaseRuneReady)
            {
                arrayAppearanceSeconds += Time.unscaledDeltaTime;
                arrayDismissing = false;
            }
            else if (arrayDismissing)
            {
                arrayDismissSeconds += Time.unscaledDeltaTime;
                if (arrayDismissSeconds >= ArrayDismissDuration)
                {
                    arrayDismissing = false;
                }
            }

            if (castController.Stage != RuneCastStage.BaseRuneReady)
            {
                hasSelectionAnchor = false;
                selectionArrayHeightPixels = 0f;
            }
            else if (!hasSelectionAnchor && castController.HasActiveHand)
            {
                selectionAnchor = RuneFollowTracker.CalculateTarget(
                    castController.ActiveHand);
                hasSelectionAnchor = true;
            }

            var target = showRune
                ? ResolveAnchor(
                    castController.ActiveHand,
                    castController.Stage,
                    castController.WidthToHeight)
                : Vector3.zero;

            tracker.UpdatePose(showRune, target, now);

            if (!tracker.Visible)
            {
                hasRenderedTarget = false;
                hasRenderedSpan = false;
                hasReferenceFingerDepth = false;
                renderedDepthScale = 1f;
                return;
            }

            // Slower than the position follow: the span comes from joints the
            // tracker jitters, and a rune that breathes with every frame is more
            // distracting than one a beat behind.
            if (showRune)
            {
                var targetSpan = HandGeometry.CalculateFingerSpan(
                    castController.ActiveHand,
                    castController.WidthToHeight,
                    runeDepthWeight);

                if (hasRenderedSpan)
                {
                    var sizeBlend = 1f - Mathf.Exp(
                        -Time.unscaledDeltaTime / Mathf.Max(sizeFollowSeconds, 0.001f));
                    renderedSpan = Mathf.Lerp(renderedSpan, targetSpan, sizeBlend);
                }
                else
                {
                    renderedSpan = targetSpan;
                    hasRenderedSpan = true;
                }

                // MediaPipe fingertip z becomes smaller as the fingers move
                // toward the camera. Capture the depth at summon time, then
                // turn relative z travel into a bounded, smoothed rune scale.
                // This applies to both the blank rune and the selected rune.
                var indexDepth = HandGeometry.GetPosition(
                    castController.ActiveHand, HandLandmarkIndex.IndexTip).z;
                var middleDepth = HandGeometry.GetPosition(
                    castController.ActiveHand, HandLandmarkIndex.MiddleTip).z;
                var fingerDepth = (indexDepth + middleDepth) * 0.5f;
                if (!hasReferenceFingerDepth)
                {
                    referenceFingerDepth = fingerDepth;
                    renderedDepthScale = 1f;
                    hasReferenceFingerDepth = true;
                }

                var targetDepthScale = Mathf.Clamp(
                    1f + (referenceFingerDepth - fingerDepth) * depthScaleSensitivity,
                    minimumDepthScale,
                    maximumDepthScale);
                var depthBlend = 1f - Mathf.Exp(
                    -Time.unscaledDeltaTime / Mathf.Max(sizeFollowSeconds, 0.001f));
                renderedDepthScale = Mathf.Lerp(
                    renderedDepthScale, targetDepthScale, depthBlend);
            }

            if (!hasRenderedTarget)
            {
                renderedTarget = tracker.Target;
                hasRenderedTarget = true;
                return;
            }

            // Once the selection circle appears its centre is frozen. The
            // hand only moves the blank rune toward the chosen node; the array
            // itself never follows the hand. This assignment must happen after
            // the normal follow interpolation, otherwise the interpolation
            // would overwrite the captured centre every frame.
            if (castController.Stage == RuneCastStage.BaseRuneReady && hasSelectionAnchor)
            {
                renderedTarget = selectionAnchor;
            }
            else
            {
                var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / FollowSeconds);
                renderedTarget = Vector3.Lerp(renderedTarget, tracker.Target, blend);
            }
        }

        // The rune rides ahead of the fingertips while it is being aimed, and
        // moves in front of the palm once the hand opens for the release: the
        // fingers it was hanging off are gone by then.
        private static Vector3 ResolveAnchor(HandData hand, RuneCastStage stage, float widthToHeight)
        {
            if (hand == null || !hand.IsValid)
            {
                return Vector3.zero;
            }

            return stage == RuneCastStage.Releasing
                ? RuneFollowTracker.CalculatePalmAnchor(hand, widthToHeight)
                : RuneFollowTracker.CalculateTarget(hand);
        }

        private void OnGUI()
        {
            // The depth must be assigned once at the top of every OnGUI pass.
            // IMGUI applies a depth change made mid-pass only on the next pass
            // and sorts on the value the pass ends with, so the earlier
            // set-then-restore pattern left this component at depth 0 and the
            // full-screen camera overlay painted straight over the rune.
            GUI.depth = -99;

            try
            {
                DrawRune();
            }
            catch (System.Exception exception)
            {
                DrawState = "error: " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void DrawRune()
        {
            if (castController == null)
            {
                DrawState = "no-controller";
                return;
            }

            if (cameraManager == null)
            {
                DrawState = "no-camera";
                return;
            }

            var keepSelectionVisible = castController.Stage == RuneCastStage.BaseRuneReady &&
                                       hasSelectionAnchor;
            if (!castController.HasPendingRune ||
                (!castController.HasActiveHand && !keepSelectionVisible))
            {
                DrawState = $"stage={castController.Stage}";
                return;
            }

            if (!hasRenderedTarget)
            {
                DrawState = "no-anchor";
                return;
            }

            var texture = ResolveTexture(castController.Element);
            if (texture == null)
            {
                DrawState = "no-rune-texture";
                return;
            }

            var cameraTexture = cameraManager.Texture;
            if (cameraTexture == null || cameraTexture.width <= 16 || cameraTexture.height <= 16)
            {
                DrawState = "no-texture";
                return;
            }

            var safe = Screen.safeArea;
            var viewport = new Rect(safe.xMin, Screen.height - safe.yMax, safe.width, safe.height);
            var rotation = HandTrackingFrameTransform.NormalizeRotation(
                cameraTexture.videoRotationAngle);
            var content = HandTrackingOverlayLayout.Fit(
                viewport, cameraTexture.width, cameraTexture.height, rotation);
            var normalized = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                renderedTarget, rotation, cameraManager.IsFrontFacing,
                cameraTexture.videoVerticallyMirrored);
            var arrayCenter = new Vector2(
                content.xMin + Mathf.Clamp01(normalized.x) * content.width,
                content.yMin + Mathf.Clamp01(normalized.y) * content.height);
            var runeCenter = arrayCenter;

            // The span is a fraction of the picture's height, so it has to be
            // scaled by however many pixels that height covers on this screen;
            // which axis that is depends on how the frame is rotated.
            var unitPixels = rotation == 0 || rotation == 180
                ? content.height
                : content.width;
            var baseHeight = Mathf.Clamp(
                renderedSpan * unitPixels * runeHeightScale,
                minimumRuneHeightPixels,
                maximumRuneHeightPixels);
            var runeHeight = Mathf.Clamp(
                baseHeight * renderedDepthScale * heldRuneScale,
                minimumRuneHeightPixels,
                maximumRuneHeightPixels);
            var arrayHeight = baseHeight;

            if (castController.Stage == RuneCastStage.BaseRuneReady)
            {
                if (selectionArrayHeightPixels <= 0f)
                {
                    selectionArrayHeightPixels = baseHeight;
                }

                arrayHeight = selectionArrayHeightPixels;
                var shortSidePixels = Mathf.Max(Mathf.Min(Screen.width, Screen.height), 1);
                var travelPixels = castController.SelectionTravel * shortSidePixels;
                var maximumTravel = arrayHeight * 0.92f;
                if (travelPixels.magnitude > maximumTravel)
                {
                    travelPixels = travelPixels.normalized * maximumTravel;
                }

                runeCenter += travelPixels;
            }

            if (castController.Stage == RuneCastStage.BaseRuneReady)
            {
                lastArrayCenter = arrayCenter;
                lastArrayHeight = arrayHeight;
            }

            // Fire is intentionally lifted as a grouped visual: the rune and
            // its flames move together, while the captured array centre stays
            // fixed. GUI coordinates grow downward, so subtracting moves up.
            if (castController.Element == RuneElement.Fire)
            {
                var density = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1f,
                    1f, 3f);
                runeCenter.y -= FireRuneLiftSp * density;
            }

            // Width keeps the artwork's proportions, so a new talisman drawing
            // only has to be dropped in and it is laid out correctly.
            var aspect = texture.height > 0
                ? (float)texture.width / texture.height
                : 0.5f;
            var width = runeHeight * aspect;
            // The phone's camera rotation is sensor-relative, so it is not the
            // layout orientation. The portrait artwork is the baseline; the
            // sensed gravity supplies one group rotation for the rune and the
            // selection array.
            var layoutRotation = GetPortraitBaseLayoutRotation();
            var previousLayoutMatrix = GUI.matrix;
            if (!Mathf.Approximately(layoutRotation, 0f))
            {
                GUIUtility.RotateAroundPivot(layoutRotation, arrayCenter);
            }

            if (castController.Stage == RuneCastStage.BaseRuneReady || arrayDismissing)
            {
                // The array centre stays captured. Its geometry and node
                // artwork share the same group rotation; only the blank rune
                // consumes SelectionTravel.
                var dismissCenter = arrayDismissing ? lastArrayCenter : arrayCenter;
                var dismissHeight = arrayDismissing ? lastArrayHeight : arrayHeight;
                DrawSelectionCircle(dismissCenter, dismissHeight, arrayDismissing);
            }

            var runeRect = new Rect(runeCenter.x - width * 0.5f,
                runeCenter.y - runeHeight * 0.5f, width, runeHeight);
            var transition = Mathf.Clamp01(elementTransitionSeconds / ElementTransitionDuration);
            var runeAppearance = castController.Stage == RuneCastStage.BaseRuneReady
                // The blank talisman is part of the summon, so it fades in
                // on the same 0.4s clock as the array instead of appearing
                // after the array has already finished.
                ? Phase01(arrayAppearanceSeconds, 0f, ArrayAppearanceDuration)
                : 1f;

            if (castController.Element == RuneElement.Fire)
            {
                RecordFireTrail(runeCenter, runeHeight);
                DrawFireTrail(runeHeight, transition);
                DrawBurningFireRune(texture, runeRect, elementTransitionSeconds);
            }
            else
            {
                DrawRuneTexture(texture, runeRect, (0.9f +
                    0.06f * Mathf.Sin(Time.unscaledTime * 3f)) * runeAppearance, 0f);
            }

            // Draw after the talisman so the fire visibly burns over its paper
            // rather than sitting behind it. The same lifted centre keeps all
            // fire layers aligned with the fire rune during conversion.
            if (castController.Element == RuneElement.Fire)
            {
                DrawFireVfx(runeCenter, runeHeight, transition, 0f);
            }
            else if (castController.Element == RuneElement.Thunder)
            {
                DrawThunderVfx(runeCenter, runeHeight, transition);
            }
            else if (castController.Element == RuneElement.Water)
            {
                DrawWaterVfx(runeCenter, runeHeight, transition);
            }
            else if (castController.Element == RuneElement.Wind)
            {
                DrawWindVfx(runeCenter, runeHeight, transition);
            }

            GUI.matrix = previousLayoutMatrix;

            DrawState = $"stage={castController.Stage} element={castController.Element} " +
                        $"layout={layoutRotation:F0} device={effectiveDeviceOrientation} " +
                        $"gravity={filteredGravity.x:F2}/{filteredGravity.y:F2} cam={rotation} " +
                        $"array={Mathf.Clamp01(arrayAppearanceSeconds / ArrayAppearanceDuration):F2} " +
                        $"burn={Mathf.Clamp01(elementTransitionSeconds / FireBurnSeconds):F2} " +
                        $"screen={Screen.width}x{Screen.height} " +
                        $"drew {runeCenter.x:F0}/{runeCenter.y:F0} h{runeHeight:F0}";
        }

        private void DrawBurningFireRune(Texture2D texture, Rect rect, float elapsedSeconds)
        {
            var reveal = Mathf.Clamp01(elapsedSeconds / 0.18f);
            var blank = ResolveTexture(RuneElement.None);
            DrawRuneTexture(blank, rect, 0.92f * (1f - reveal), 0f);

            var burn = Mathf.Clamp01(elapsedSeconds / FireBurnSeconds);
            var remaining = 1f - burn;
            if (texture != null && remaining > 0.001f)
            {
                // Crop from the bottom upward. The destination rect also
                // shrinks upward so no transparent burned paper remains.
                var remainingRect = new Rect(rect.x, rect.y, rect.width,
                    rect.height * remaining);
                var remainingUv = new Rect(0f, burn, 1f, remaining);
                GUI.color = new Color(1f, 1f, 1f, 0.92f * reveal);
                GUI.DrawTextureWithTexCoords(remainingRect, texture, remainingUv, true);
                GUI.color = Color.white;

                // The paper immediately behind the combustion front chars
                // before it disappears, avoiding a clean digital wipe.
                DrawBurntPaperOverlay(rect, burn, reveal);
            }

            if (burn <= 0f || burn >= 1f)
            {
                return;
            }

            // A bright moving combustion front prevents the crop from reading
            // as a simple wipe. Small animated flame cells sit across the edge.
            var edgeY = rect.y + rect.height * remaining;
            DrawLine(new Vector2(rect.x, edgeY), new Vector2(rect.xMax, edgeY),
                new Color(1f, 0.72f, 0.12f, 0.92f), Mathf.Max(2f, rect.height * 0.018f));
            for (var index = 0; index < 3; index++)
            {
                var x = Mathf.Lerp(rect.x, rect.xMax, (index + 0.5f) / 3f);
                var size = rect.height * (0.16f + index * 0.018f);
                var flameRect = new Rect(x - size * 0.5f, edgeY - size * 0.72f,
                    size, size);
                var frame = (Mathf.FloorToInt(Time.unscaledTime * 18f) + index * 5) % 16;
                DrawSpriteSheetFrame(fireCoreVfxTexture, flameRect, frame,
                    0.9f * reveal, 0f);
            }
        }

        private void DrawBurntPaperOverlay(Rect rect, float burn, float reveal)
        {
            if (burn <= 0.02f || reveal <= 0f)
            {
                return;
            }

            var edgeY = rect.y + rect.height * (1f - burn);
            var charDepth = rect.height * Mathf.Lerp(0.035f, 0.12f, burn);
            var bands = 7;
            for (var index = 0; index < bands; index++)
            {
                var t = (index + 0.5f) / bands;
                var y = edgeY + charDepth * t;
                var wave = Mathf.Sin(Time.unscaledTime * 8f + index * 1.73f) * rect.width * 0.018f;
                var bandRect = new Rect(rect.x + rect.width * 0.025f,
                    y, rect.width * 0.95f, charDepth / bands * 1.35f);
                DrawFilledRect(bandRect, new Color(0.08f, 0.025f, 0.008f,
                    (0.12f + 0.28f * t) * reveal));
                DrawLine(new Vector2(rect.x + rect.width * 0.08f, y + wave),
                    new Vector2(rect.xMax - rect.width * 0.08f, y - wave),
                    new Color(0.22f, 0.045f, 0.01f, 0.22f * reveal),
                    Mathf.Max(1f, rect.height * 0.008f));
            }

            // Scorched edge: a broken dark rim with a hot red-orange inner edge.
            for (var index = 0; index < 9; index++)
            {
                var x = Mathf.Lerp(rect.x, rect.xMax, (index + 0.5f) / 9f);
                var wobble = Mathf.Sin(Time.unscaledTime * 10f + index * 2.1f) * rect.height * 0.018f;
                var edge = new Vector2(x, edgeY + wobble);
                DrawLine(edge, edge + new Vector2(rect.width / 11f, rect.height * 0.006f),
                    new Color(0.12f, 0.018f, 0.004f, 0.86f * reveal),
                    Mathf.Max(2f, rect.height * 0.014f));
                DrawLine(edge + new Vector2(0f, rect.height * 0.018f),
                    edge + new Vector2(rect.width / 12f, rect.height * 0.018f),
                    new Color(1f, 0.22f, 0.025f, 0.48f * reveal),
                    Mathf.Max(1f, rect.height * 0.007f));
            }
        }

        private void RecordFireTrail(Vector2 position, float runeHeight)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (fireTrailCount > 0 &&
                now - lastFireTrailSampleAt < 0.025f)
            {
                return;
            }

            if (fireTrailCount > 0 &&
                Vector2.Distance(position, lastFireTrailPosition) < runeHeight * 0.045f)
            {
                return;
            }

            fireTrailPositions[fireTrailWriteIndex] = position;
            fireTrailTimes[fireTrailWriteIndex] = now;
            fireTrailWriteIndex = (fireTrailWriteIndex + 1) % FireTrailCapacity;
            fireTrailCount = Mathf.Min(fireTrailCount + 1, FireTrailCapacity);
            lastFireTrailSampleAt = now;
            lastFireTrailPosition = position;
        }

        private void DrawFireTrail(float runeHeight, float intensity)
        {
            var now = Time.unscaledTime;
            var growthProgress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elementTransitionSeconds / FireBurnSeconds));
            var growth = Mathf.Lerp(FireInitialGrowth, 1f, growthProgress);
            for (var offset = 1; offset <= fireTrailCount; offset++)
            {
                var index = (fireTrailWriteIndex - offset + FireTrailCapacity) %
                            FireTrailCapacity;
                var age = now - fireTrailTimes[index];
                if (age < 0f || age > FireTrailSeconds)
                {
                    continue;
                }

                var life = 1f - age / FireTrailSeconds;
                var scale = Mathf.Lerp(0.58f, 0.92f, life);
                var trailWidth = runeHeight * 1.08f * growth * scale;
                var trailHeight = runeHeight * FireFinalHeightScale * growth * scale;
                var trailBottom = fireTrailPositions[index].y + runeHeight * 0.48f;
                var rect = new Rect(
                    fireTrailPositions[index].x - trailWidth * 0.5f,
                    trailBottom - trailHeight,
                    trailWidth, trailHeight);
                var frame = (Mathf.FloorToInt(now * 14f) + offset * 3) % 16;
                DrawSpriteSheetFrame(fireOuterVfxTexture, rect, frame,
                    0.34f * life * intensity, 0f);
            }
        }

        private void ResetFireTrail()
        {
            fireTrailCount = 0;
            fireTrailWriteIndex = 0;
            lastFireTrailSampleAt = float.NegativeInfinity;
            lastFireTrailPosition = Vector2.zero;
        }

        private static void DrawRuneTexture(Texture2D texture, Rect rect, float alpha,
            float rotation)
        {
            if (texture == null || alpha <= 0f)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (!Mathf.Approximately(rotation, 0f))
            {
                GUIUtility.RotateAroundPivot(rotation, rect.center);
            }
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = previousMatrix;
            GUI.color = Color.white;
        }

        private void DrawFireVfx(Vector2 center, float height, float intensity,
            float rotation)
        {
            if (intensity <= 0f)
            {
                return;
            }

            var time = Time.unscaledTime;
            var growthProgress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elementTransitionSeconds / FireBurnSeconds));
            var growth = Mathf.Lerp(FireInitialGrowth, 1f, growthProgress);
            var pulse = 1f + 0.06f * Mathf.Sin(time * 8f);
            // Leave a safe transparent margin around the complete flame
            // silhouette. This prevents the top tongue and bottom base from
            // being visually cropped when the sprite frame is rotated.
            var flameHeight = height * FireFinalHeightScale * growth;
            var flameWidth = height * Mathf.Lerp(0.58f, 1.08f, growthProgress);
            // Keep the base planted near the bottom of the original rune while
            // the top grows upward, matching a real flame rather than scaling
            // equally in every direction.
            const float screenMargin = 10f;
            var flameBottom = Mathf.Clamp(center.y + height * 0.48f,
                screenMargin + height * 0.18f, Screen.height - screenMargin);
            var availableHeight = Mathf.Max(height * 0.22f,
                flameBottom - screenMargin - height * 0.04f);
            if (flameHeight > availableHeight)
            {
                var fit = availableHeight / Mathf.Max(flameHeight, 1f);
                flameHeight = availableHeight;
                flameWidth *= fit;
            }
            var flameCenter = new Vector2(center.x, flameBottom - flameHeight * 0.5f);
            var glowRect = new Rect(
                flameCenter.x - flameWidth * 0.72f,
                flameCenter.y - flameHeight * 0.56f,
                flameWidth * 1.44f,
                flameHeight * 1.12f);
            DrawVfxTexture(fireGlowVfxTexture, glowRect, 0.16f * intensity, rotation);

            // The outer frame and the bright core run at different speeds and
            // scales. Their desynchronisation prevents the fire from reading as
            // one rigid animated sticker on the hand.
            var outerWidth = flameWidth * (1.04f + 0.025f * pulse);
            var outerHeight = flameHeight * (1.02f + 0.018f * pulse);
            var outerRect = new Rect(flameCenter.x - outerWidth * 0.5f,
                flameBottom - outerHeight * 1.06f, outerWidth, outerHeight * 1.06f);
            var outerFrame = Mathf.FloorToInt(time * 14f) % 16;
            DrawSpriteSheetFrame(fireOuterVfxTexture, outerRect, outerFrame,
                0.82f * intensity, rotation);

            // The source flame sheets have deliberately soft alpha, which can
            // leave a camera-visible hole once the paper has burned away. Lay
            // down a compact, fully opaque hot core before the animated inner
            // flame so the fire reads as a dense volume rather than a ring.
            DrawFireSolidCore(flameCenter, flameWidth, flameHeight, intensity);

            // Six small atlas cells provide a sparse, deterministic ember field.
            // Their phase is time-based, so no per-frame allocations are needed.
            for (var index = 0; index < 6; index++)
            {
                var phase = time * (0.7f + index * 0.08f) + index * 1.9f;
                var angle = phase * (42f + index * 4f) * Mathf.Deg2Rad;
                var distance = flameWidth * (0.48f + 0.16f * Mathf.Sin(phase));
                var emberCenter = flameCenter +
                    new Vector2(Mathf.Cos(angle) * distance,
                        Mathf.Sin(angle) * distance - flameHeight * 0.08f * index);
                var emberSize = height * (0.075f + 0.025f * Mathf.Sin(phase * 1.7f));
                var emberRect = new Rect(emberCenter.x - emberSize * 0.5f,
                    emberCenter.y - emberSize * 0.5f, emberSize, emberSize);
                var emberFrame = (index * 3 + Mathf.FloorToInt(time * 9f)) % 16;
                DrawSpriteSheetFrame(fireEmberVfxTexture, emberRect, emberFrame,
                    0.9f * intensity, rotation + phase * 18f);
            }
        }

        private void DrawThunderVfx(Vector2 center, float height, float intensity)
        {
            if (intensity <= 0f)
            {
                return;
            }

            var time = Time.unscaledTime;
            var pulse = 0.65f + 0.35f * Mathf.Sin(time * 15f);
            var radius = height * 0.48f;
            var color = new Color(0.35f, 0.72f, 1f, 0.82f * intensity * pulse);
            var glow = new Color(0.55f, 0.82f, 1f, 0.22f * intensity * pulse);
            if (thunderArcVfxTexture != null)
            {
                var arcSize = height * 1.35f;
                var arcRect = new Rect(center.x - arcSize * 0.5f,
                    center.y - arcSize * 0.5f, arcSize, arcSize);
                DrawSpriteSheetFrame(thunderArcVfxTexture, arcRect,
                    Mathf.FloorToInt(time * 18f) % 16, 0.82f * intensity, 0f);
            }
            for (var branch = 0; branch < 4; branch++)
            {
                var angle = time * (18f + branch * 2f) + branch * 90f;
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                var start = center + direction * (radius * 0.55f);
                var previous = start;
                for (var segment = 1; segment <= 5; segment++)
                {
                    var along = segment / 5f;
                    var jitter = new Vector2(
                        Mathf.Sin(time * 22f + branch * 3.1f + segment) * height * 0.045f,
                        Mathf.Cos(time * 19f + branch * 2.4f + segment) * height * 0.045f);
                    var next = center + direction * Mathf.Lerp(radius * 0.55f, radius, along) + jitter;
                    DrawLine(previous, next, glow, Mathf.Max(5f, height * 0.026f));
                    DrawLine(previous, next, color, Mathf.Max(1.5f, height * 0.009f));
                    previous = next;
                }
            }
        }

        private void DrawWaterVfx(Vector2 center, float height, float intensity)
        {
            if (intensity <= 0f) return;
            var time = Time.unscaledTime;
            var radius = height * 0.48f;
            var color = new Color(0.18f, 0.72f, 1f, 0.62f * intensity);
            var highlight = new Color(0.7f, 0.94f, 1f, 0.5f * intensity);
            if (waterVfxTexture != null)
            {
                var size = height * 1.22f;
                DrawSpriteSheetFrame(waterVfxTexture,
                    new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size),
                    Mathf.FloorToInt(time * 14f) % 16, 0.8f * intensity, 0f);
            }
            for (var ring = 0; ring < 2; ring++)
            {
                var previous = center + Vector2.right * (radius + ring * height * 0.08f);
                for (var i = 1; i <= 28; i++)
                {
                    var angle = i * Mathf.PI * 2f / 28f;
                    var r = radius + ring * height * 0.08f + Mathf.Sin(time * 4f + i * 0.7f) * height * 0.025f;
                    var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    DrawLine(previous, next, ring == 0 ? color : highlight,
                        Mathf.Max(1.5f, height * (ring == 0 ? 0.012f : 0.007f)));
                    previous = next;
                }
            }
            for (var i = 0; i < 6; i++)
            {
                var phase = time * (1.4f + i * 0.12f) + i * 1.1f;
                var drop = center + new Vector2(Mathf.Cos(phase) * radius * 1.05f,
                    Mathf.Sin(phase * 1.3f) * radius);
                DrawFilledCircle(drop, height * 0.018f,
                    new Color(0.45f, 0.85f, 1f, 0.65f * intensity));
            }
        }

        private void DrawWindVfx(Vector2 center, float height, float intensity)
        {
            if (intensity <= 0f) return;
            var time = Time.unscaledTime;
            var radius = height * 0.62f;
            var color = new Color(0.65f, 1f, 0.76f, 0.68f * intensity);
            if (windVfxTexture != null)
            {
                var size = height * 1.3f;
                DrawSpriteSheetFrame(windVfxTexture,
                    new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size),
                    Mathf.FloorToInt(time * 16f) % 16, 0.78f * intensity,
                    time * 18f);
            }
            for (var arc = 0; arc < 4; arc++)
            {
                var previous = center + Vector2.right * (radius * 0.35f);
                for (var i = 1; i <= 18; i++)
                {
                    var t = i / 18f;
                    var angle = time * (28f + arc * 4f) + arc * 90f + t * 115f;
                    var r = Mathf.Lerp(radius * 0.35f, radius, t);
                    var next = center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                        Mathf.Sin(angle * Mathf.Deg2Rad)) * r;
                    DrawLine(previous, next, color,
                        Mathf.Max(1.5f, height * (0.014f - t * 0.004f)));
                    previous = next;
                }
            }
        }

        private void DrawFireSolidCore(Vector2 center, float width, float height,
            float intensity)
        {
            if (intensity <= 0f)
            {
                return;
            }

            var opacity = Mathf.Clamp01(intensity * 1.65f);
            var time = Time.unscaledTime;
            var bottom = center.y + height * 0.5f;
            var frame = Mathf.FloorToInt(time * 18f) % 16;

            // The replacement sheet already contains a complete flame in each
            // frame. Draw it once at full bounds; layering old tongue/core
            // sheets here creates rectangular seams and visible cut bars.
            DrawSpriteSheetFrame(fireCoreVfxTexture,
                new Rect(center.x - width * 0.5f, bottom - height, width, height),
                frame, opacity, 0f);
            // A subdued inset pass fills transparent gaps in the generated
            // silhouette without reintroducing the old rectangular tongue.
            var insetWidth = width * 0.84f;
            var insetHeight = height * 0.88f;
            DrawSpriteSheetFrame(fireCoreVfxTexture,
                new Rect(center.x - insetWidth * 0.5f, bottom - insetHeight,
                    insetWidth, insetHeight),
                frame, opacity * 0.42f, 0f);
        }

        private static void DrawVfxTexture(Texture2D texture, Rect rect, float alpha,
            float rotation)
        {
            if (texture == null || alpha <= 0f)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (!Mathf.Approximately(rotation, 0f))
            {
                GUIUtility.RotateAroundPivot(rotation, rect.center);
            }

            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = previousMatrix;
            GUI.color = Color.white;
        }

        private static void DrawSpriteSheetFrame(Texture2D sheet, Rect rect, int frame,
            float alpha, float rotation)
        {
            if (sheet == null || alpha <= 0f)
            {
                return;
            }

            const int columns = 4;
            const int rows = 4;
            frame = Mathf.Abs(frame) % (columns * rows);
            var column = frame % columns;
            var row = frame / columns;
            var uv = new Rect(
                column / (float)columns,
                1f - (row + 1) / (float)rows,
                1f / columns,
                1f / rows);

            var previousMatrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (!Mathf.Approximately(rotation, 0f))
            {
                GUIUtility.RotateAroundPivot(rotation, rect.center);
            }

            GUI.DrawTextureWithTexCoords(rect, sheet, uv, true);
            GUI.matrix = previousMatrix;
            GUI.color = Color.white;
        }

        private void DrawFireAura(Vector2 center, float height, float intensity)
        {
            if (intensity <= 0f)
            {
                return;
            }

            var pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 7f);
            var radius = height * 0.43f * pulse;
            var flameColor = new Color(1f, 0.24f, 0.035f, 0.62f * intensity);
            var emberColor = new Color(1f, 0.72f, 0.12f, 0.78f * intensity);

            // Eight short rising flame tongues orbit the paper. They are kept
            // as line segments for the mobile graybox: no texture, shader, or
            // particle-system asset is required for the conversion proof.
            for (var index = 0; index < 8; index++)
            {
                var angle = Time.unscaledTime * (38f + index * 2f) + index * 45f;
                var radians = angle * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var tangent = new Vector2(-direction.y, direction.x);
                var basePoint = center + direction * radius;
                var tip = basePoint + direction * (height * 0.12f +
                    Mathf.Sin(Time.unscaledTime * 5f + index) * height * 0.035f) +
                    tangent * Mathf.Sin(Time.unscaledTime * 4f + index * 1.7f) * height * 0.06f;
                DrawLine(basePoint, tip, flameColor, Mathf.Max(2f, height * 0.018f));
                DrawLine(basePoint, Vector2.Lerp(basePoint, tip, 0.55f), emberColor,
                    Mathf.Max(1f, height * 0.009f));
            }

            var glow = new Color(1f, 0.18f, 0.02f, 0.07f * intensity);
            GUI.color = glow;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius,
                radius * 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private float GetPortraitBaseLayoutRotation()
        {
            // The portrait layout is the source of truth. Do not infer this
            // from Screen.orientation, screen dimensions, or camera rotation:
            // all three can remain unchanged when Android auto-rotate is off.
            switch (effectiveDeviceOrientation)
            {
                case DeviceOrientation.LandscapeLeft:
                    return 90f;
                case DeviceOrientation.LandscapeRight:
                    return -90f;
                default:
                    return 0f;
            }
        }

        private void UpdateDeviceOrientation()
        {
            // Read gravity directly so Android's automatic screen-rotation
            // preference cannot suppress the app's layout response. A short
            // hold and low-pass filter keep hand motion from flipping the UI.
            filteredGravity = Vector3.Lerp(filteredGravity, Input.acceleration,
                1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.08f));
            var sensed = ResolveOrientationFromGravity(filteredGravity);
            if (sensed == DeviceOrientation.Unknown)
            {
                var reported = Input.deviceOrientation;
                if (reported == DeviceOrientation.Portrait ||
                    reported == DeviceOrientation.LandscapeLeft ||
                    reported == DeviceOrientation.LandscapeRight)
                {
                    sensed = reported;
                }
            }

            if (sensed == DeviceOrientation.Unknown)
            {
                return;
            }

            if (sensed != candidateDeviceOrientation)
            {
                candidateDeviceOrientation = sensed;
                orientationCandidateSince = Time.unscaledTime;
                return;
            }

            if (sensed != effectiveDeviceOrientation &&
                Time.unscaledTime - orientationCandidateSince >= OrientationHoldSeconds)
            {
                effectiveDeviceOrientation = sensed;
            }
        }

        private static DeviceOrientation ResolveOrientationFromGravity(Vector3 gravity)
        {
            const float minimumGravity = 0.52f;
            const float dominance = 1.12f;
            var absoluteX = Mathf.Abs(gravity.x);
            var absoluteY = Mathf.Abs(gravity.y);
            if (absoluteX >= minimumGravity && absoluteX >= absoluteY * dominance)
            {
                return gravity.x < 0f
                    ? DeviceOrientation.LandscapeLeft
                    : DeviceOrientation.LandscapeRight;
            }

            if (-gravity.y >= minimumGravity && absoluteY >= absoluteX * dominance)
            {
                return DeviceOrientation.Portrait;
            }

            return DeviceOrientation.Unknown;
        }

        private void DrawSelectionCircle(Vector2 center, float runeHeight,
            bool dismissing = false)
        {
            var appearance = Mathf.Clamp01(arrayAppearanceSeconds / ArrayAppearanceDuration);
            if (dismissing)
            {
                var exit = Mathf.Clamp01(arrayDismissSeconds / ArrayDismissDuration);
                appearance = 1f - Mathf.SmoothStep(0f, 1f, exit);
            }
            var easedAppearance = Mathf.SmoothStep(0f, 1f, appearance);
            var pulse = 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var radius = runeHeight * 0.92f * pulse * Mathf.Lerp(0.42f, 1f, easedAppearance);
            var innerRadius = radius * 0.68f;
            var rotation = Time.unscaledTime * 12f;
            var energyPhase = Phase01(arrayAppearanceSeconds, 0f, 0.14f);
            var innerPhase = Phase01(arrayAppearanceSeconds, 0.04f, 0.18f);
            var outerPhase = Phase01(arrayAppearanceSeconds, 0.08f, 0.18f);
            var nodePhase = Phase01(arrayAppearanceSeconds, 0.18f, 0.16f);
            energyPhase *= appearance;
            innerPhase *= appearance;
            outerPhase *= appearance;
            nodePhase *= appearance;

            var layerSize = radius * 2.48f;
            var layerRect = new Rect(center.x - layerSize * 0.5f,
                center.y - layerSize * 0.5f, layerSize, layerSize);
            DrawVfxTexture(arraySummonVfxTexture, layerRect,
                0.56f * energyPhase * (1f - 0.35f * easedAppearance), -rotation * 0.4f);

            var innerSize = radius * 1.84f;
            var innerRect = new Rect(center.x - innerSize * 0.5f,
                center.y - innerSize * 0.5f, innerSize, innerSize);
            DrawVfxTexture(arrayInnerVfxTexture, innerRect,
                0.72f * innerPhase, -Time.unscaledTime * 8f);

            DrawFilledCircle(center, radius,
                new Color(0f, 0f, 0f, 0.1f * easedAppearance));
            var nodeSize = Mathf.Clamp(runeHeight * 0.34f, 42f, 150f);
            var nodeMaskRadius = nodeSize * 0.55f;
            DrawMaskedCircle(center, radius,
                new Color(1f, 0.78f, 0.3f, 0.38f * outerPhase), 6f,
                radius, nodeMaskRadius);
            DrawMaskedCircle(center, innerRadius,
                new Color(1f, 0.9f, 0.55f, 0.22f * innerPhase), 4f,
                radius, nodeMaskRadius);
            DrawMaskedCircle(center, radius * 0.48f,
                new Color(1f, 0.72f, 0.2f, 0.18f * innerPhase), 4f,
                radius, nodeMaskRadius);

            // Two counter-rotating tick rings make the selection state legible
            // even when the four talisman textures are partially transparent.
            for (var index = 0; index < 8; index++)
            {
                var angle = rotation + index * 45f;
                DrawRadialMark(center, radius, angle, 11f, 1.6f,
                    new Color(1f, 0.82f, 0.35f, 0.42f * outerPhase));
                DrawRadialMark(center, innerRadius, -rotation * 1.35f + index * 45f,
                    7f, 1.2f, new Color(1f, 0.95f, 0.7f, 0.26f * innerPhase));
            }

            // Keep this mapping identical to DirectionSelectionRecognizer:
            // screen up = thunder, down = fire, left = water, right = wind.
            // IMGUI y increases downwards, so visual up is -y.
            DrawElementNode(center + NodeOffset(ScreenDirection.Up, radius), nodeSize,
                arrayThunderRuneTexture, 0f, nodePhase);
            DrawElementNode(center + NodeOffset(ScreenDirection.Down, radius), nodeSize,
                arrayFireRuneTexture, 0f, nodePhase);
            DrawElementNode(center + NodeOffset(ScreenDirection.Left, radius), nodeSize,
                arrayWaterRuneTexture, 0f, nodePhase);
            DrawElementNode(center + NodeOffset(ScreenDirection.Right, radius), nodeSize,
                arrayWindRuneTexture, 0f, nodePhase);
        }

        private static float Phase01(float elapsed, float start, float duration)
        {
            return Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((elapsed - start) / Mathf.Max(duration, 0.001f)));
        }

        private enum ScreenDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        private static Vector2 NodeOffset(ScreenDirection direction, float radius)
        {
            switch (direction)
            {
                case ScreenDirection.Up:
                    return new Vector2(0f, -radius);
                case ScreenDirection.Down:
                    return new Vector2(0f, radius);
                case ScreenDirection.Left:
                    return new Vector2(-radius, 0f);
                default:
                    return new Vector2(radius, 0f);
            }
        }

        private void DrawMaskedCircle(Vector2 center, float radius, Color color,
            float thickness, float nodeOrbitRadius, float nodeMaskRadius)
        {
            const int segments = 64;
            var previous = center + Vector2.right * radius;
            for (var index = 1; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var midpoint = (previous + next) * 0.5f;
                var hidden = Vector2.Distance(midpoint,
                    center + NodeOffset(ScreenDirection.Up, nodeOrbitRadius)) <= nodeMaskRadius
                    || Vector2.Distance(midpoint,
                        center + NodeOffset(ScreenDirection.Down, nodeOrbitRadius)) <= nodeMaskRadius
                    || Vector2.Distance(midpoint,
                        center + NodeOffset(ScreenDirection.Left, nodeOrbitRadius)) <= nodeMaskRadius
                    || Vector2.Distance(midpoint,
                        center + NodeOffset(ScreenDirection.Right, nodeOrbitRadius)) <= nodeMaskRadius;
                if (!hidden)
                {
                    DrawLine(previous, next, color, thickness);
                }
                previous = next;
            }
        }

        private void DrawElementNode(Vector2 center, float size, Texture2D texture,
            float rotation, float appearance)
        {
            if (texture == null)
            {
                return;
            }

            // Mask the rotating array underneath this node before drawing the
            // talisman. It is neutral rather than element-coloured, so the four
            // node artworks remain transparent and visually consistent.
            DrawFilledCircle(center, size * 0.53f,
                new Color(0f, 0f, 0f, 0.15f * appearance));

            var previousMatrix = GUI.matrix;
            var alpha = (0.48f + 0.04f * Mathf.Sin(
                Time.unscaledTime * 3.1f + center.x)) * appearance;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var runeRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f,
                size, size);
            if (!Mathf.Approximately(rotation, 0f))
            {
                GUIUtility.RotateAroundPivot(rotation, runeRect.center);
            }
            GUI.DrawTexture(runeRect, texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = previousMatrix;
            GUI.color = Color.white;
            DrawCircle(center, size * 0.54f,
                new Color(1f, 0.82f, 0.4f, 0.48f * appearance), 3f);
        }

        private void DrawCircle(Vector2 center, float radius, Color color, float thickness)
        {
            const int segments = 64;
            var previous = center + Vector2.right * radius;
            for (var index = 1; index <= segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawLine(previous, next, color, thickness);
                previous = next;
            }
        }

        private static void DrawFilledCircle(Vector2 center, float radius, Color color)
        {
            const int horizontalSlices = 64;
            var diameter = radius * 2f;
            var sliceHeight = diameter / horizontalSlices;
            GUI.color = color;
            for (var index = 0; index < horizontalSlices; index++)
            {
                var y = -radius + (index + 0.5f) * sliceHeight;
                var halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y));
                GUI.DrawTexture(
                    new Rect(center.x - halfWidth, center.y + y - sliceHeight * 0.5f,
                        halfWidth * 2f, sliceHeight + 0.5f),
                    Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private static void DrawFilledRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawRadialMark(Vector2 center, float radius, float angleDegrees,
            float length, float thickness, Color color)
        {
            var angle = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            var start = center + direction * (radius - length);
            DrawLine(start, start + direction * length, color, thickness);
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var previous = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness),
                Texture2D.whiteTexture);
            GUI.matrix = previous;
            GUI.color = Color.white;
        }

        private Texture2D ResolveTexture(RuneElement element)
        {
            return element switch
            {
                RuneElement.Thunder => thunderRuneTexture,
                RuneElement.Fire => fireRuneTexture,
                RuneElement.Water => waterRuneTexture,
                RuneElement.Wind => windRuneTexture,
                _ => blankRuneTexture
            };
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleResult);
            }
        }
    }
}
