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
        public const string BuildMarker = "RF-CHECK-B6";

        private const float FollowSeconds = 0.035f;
        private const float LostResultSeconds = 0.35f;
        private const float FireRuneLiftSp = 20f;

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
        private double lastResultAt = double.NegativeInfinity;
        private float lastWidthToHeight = 16f / 9f;
        private DeviceOrientation effectiveDeviceOrientation = DeviceOrientation.Portrait;
        private DeviceOrientation candidateDeviceOrientation = DeviceOrientation.Portrait;
        private Vector3 filteredGravity = Vector3.down;
        private float orientationCandidateSince;
        private const float OrientationHoldSeconds = 0.12f;

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
                renderedElement = element;
                elementTransitionSeconds = 0f;
            }

            elementTransitionSeconds += Time.unscaledDeltaTime;

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

            if (!castController.HasPendingRune || !castController.HasActiveHand)
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

            if (castController.Stage == RuneCastStage.BaseRuneReady)
            {
                // The array centre stays captured. Its geometry and node
                // artwork share the same group rotation; only the blank rune
                // consumes SelectionTravel.
                DrawSelectionCircle(arrayCenter, arrayHeight);
            }

            var runeRect = new Rect(runeCenter.x - width * 0.5f,
                runeCenter.y - runeHeight * 0.5f, width, runeHeight);
            var transition = Mathf.Clamp01(elementTransitionSeconds / ElementTransitionDuration);

            if (castController.Element == RuneElement.Fire && transition < 1f)
            {
                var blank = ResolveTexture(RuneElement.None);
                DrawRuneTexture(blank, runeRect, 0.92f * (1f - transition), 0f);
                DrawRuneTexture(texture, runeRect, 0.92f * transition, 0f);
            }
            else
            {
                DrawRuneTexture(texture, runeRect, 0.9f +
                    0.06f * Mathf.Sin(Time.unscaledTime * 3f), 0f);
            }

            // Draw after the talisman so the fire visibly burns over its paper
            // rather than sitting behind it. The same lifted centre keeps all
            // fire layers aligned with the fire rune during conversion.
            if (castController.Element == RuneElement.Fire)
            {
                DrawFireVfx(runeCenter, runeHeight, transition, 0f);
            }

            GUI.matrix = previousLayoutMatrix;

            DrawState = $"stage={castController.Stage} element={castController.Element} " +
                        $"layout={layoutRotation:F0} device={effectiveDeviceOrientation} " +
                        $"gravity={filteredGravity.x:F2}/{filteredGravity.y:F2} cam={rotation} " +
                        $"screen={Screen.width}x{Screen.height} " +
                        $"drew {runeCenter.x:F0}/{runeCenter.y:F0} h{runeHeight:F0}";
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
            var pulse = 1f + 0.06f * Mathf.Sin(time * 8f);
            var glowRect = new Rect(center.x - height * 0.78f, center.y - height * 0.78f,
                height * 1.56f, height * 1.56f);
            DrawVfxTexture(fireGlowVfxTexture, glowRect, 0.16f * intensity, rotation);

            // The outer frame and the bright core run at different speeds and
            // scales. Their desynchronisation prevents the fire from reading as
            // one rigid animated sticker on the hand.
            var outerSize = height * (1.13f + 0.025f * pulse);
            var outerRect = new Rect(center.x - outerSize * 0.5f,
                center.y - outerSize * 0.5f, outerSize, outerSize);
            var outerFrame = Mathf.FloorToInt(time * 14f) % 16;
            DrawSpriteSheetFrame(fireOuterVfxTexture, outerRect, outerFrame,
                0.82f * intensity, rotation);

            var coreSize = height * (0.92f + 0.035f * Mathf.Sin(time * 10f));
            var coreRect = new Rect(center.x - coreSize * 0.5f,
                center.y - coreSize * 0.5f, coreSize, coreSize);
            var coreFrame = Mathf.FloorToInt(time * 18f + 4f) % 16;
            DrawSpriteSheetFrame(fireCoreVfxTexture, coreRect, coreFrame,
                0.72f * intensity, rotation);

            // Six small atlas cells provide a sparse, deterministic ember field.
            // Their phase is time-based, so no per-frame allocations are needed.
            for (var index = 0; index < 6; index++)
            {
                var phase = time * (0.7f + index * 0.08f) + index * 1.9f;
                var angle = phase * (42f + index * 4f) * Mathf.Deg2Rad;
                var distance = height * (0.48f + 0.16f * Mathf.Sin(phase));
                var emberCenter = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var emberSize = height * (0.075f + 0.025f * Mathf.Sin(phase * 1.7f));
                var emberRect = new Rect(emberCenter.x - emberSize * 0.5f,
                    emberCenter.y - emberSize * 0.5f, emberSize, emberSize);
                var emberFrame = (index * 3 + Mathf.FloorToInt(time * 9f)) % 16;
                DrawSpriteSheetFrame(fireEmberVfxTexture, emberRect, emberFrame,
                    0.9f * intensity, rotation + phase * 18f);
            }
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

        private void DrawSelectionCircle(Vector2 center, float runeHeight)
        {
            var pulse = 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var radius = runeHeight * 0.92f * pulse;
            var innerRadius = radius * 0.68f;
            var rotation = Time.unscaledTime * 12f;

            DrawFilledCircle(center, radius, new Color(0f, 0f, 0f, 0.1f));
            DrawCircle(center, radius, new Color(1f, 0.78f, 0.3f, 0.48f), 8f);
            DrawCircle(center, innerRadius, new Color(1f, 0.9f, 0.55f, 0.28f), 6f);
            DrawCircle(center, radius * 0.48f,
                new Color(1f, 0.72f, 0.2f, 0.22f), 4f);

            // Two counter-rotating tick rings make the selection state legible
            // even when the four talisman textures are partially transparent.
            for (var index = 0; index < 12; index++)
            {
                var angle = rotation + index * 30f;
                DrawRadialMark(center, radius, angle, 14f, 2f,
                    new Color(1f, 0.82f, 0.35f, 0.62f));
                DrawRadialMark(center, innerRadius, -rotation * 1.35f + index * 30f,
                    8f, 1.5f, new Color(1f, 0.95f, 0.7f, 0.38f));
            }

            var nodeSize = Mathf.Clamp(runeHeight * 0.34f, 42f, 150f);
            // Keep this mapping identical to DirectionSelectionRecognizer:
            // screen up = thunder, down = fire, left = water, right = wind.
            // IMGUI y increases downwards, so visual up is -y.
            DrawElementNode(center + NodeOffset(ScreenDirection.Up, radius), nodeSize,
                arrayThunderRuneTexture,
                new Color(0.52f, 0.42f, 1f, 0.82f), 0f);
            DrawElementNode(center + NodeOffset(ScreenDirection.Down, radius), nodeSize,
                arrayFireRuneTexture,
                new Color(1f, 0.38f, 0.1f, 0.84f), 0f);
            DrawElementNode(center + NodeOffset(ScreenDirection.Left, radius), nodeSize,
                arrayWaterRuneTexture,
                new Color(0.2f, 0.72f, 1f, 0.82f), 0f);
            DrawElementNode(center + NodeOffset(ScreenDirection.Right, radius), nodeSize,
                arrayWindRuneTexture,
                new Color(0.42f, 1f, 0.48f, 0.82f), 0f);
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

        private void DrawElementNode(Vector2 center, float size, Texture2D texture, Color tint,
            float rotation)
        {
            if (texture == null)
            {
                return;
            }

            var glow = new Color(tint.r, tint.g, tint.b, 0.12f +
                0.04f * Mathf.Sin(Time.unscaledTime * 3.1f + center.x));
            GUI.color = glow;
            GUI.DrawTexture(new Rect(center.x - size * 0.62f, center.y - size * 0.62f,
                size * 1.24f, size * 1.24f), Texture2D.whiteTexture);
            var previousMatrix = GUI.matrix;
            GUI.color = tint;
            var runeRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f,
                size, size);
            if (!Mathf.Approximately(rotation, 0f))
            {
                GUIUtility.RotateAroundPivot(rotation, runeRect.center);
            }
            GUI.DrawTexture(runeRect, texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = previousMatrix;
            DrawCircle(center, size * 0.54f,
                new Color(tint.r, tint.g, tint.b, Mathf.Max(tint.a, 0.72f)), 3f);
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
