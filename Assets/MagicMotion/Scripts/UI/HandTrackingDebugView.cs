using System.Collections.Generic;
using MagicMotion.Camera;
using MagicMotion.Core;
using MagicMotion.Gesture;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.UI
{
    [DisallowMultipleComponent]
    public sealed class HandTrackingDebugView : MonoBehaviour
    {
        private static readonly int[] BonePairs =
        {
            0, 1, 1, 2, 2, 3, 3, 4,
            0, 5, 5, 6, 6, 7, 7, 8,
            5, 9, 9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16,
            13, 17, 17, 18, 18, 19, 19, 20,
            0, 17
        };

        private const float LostHoldSeconds = 0.5f;
        private const float LostFadeSeconds = 0.35f;

        [SerializeField] private bool visible = true;
        private readonly List<HandData> displayedHands = new List<HandData>(2);
        private readonly Dictionary<Handedness, RuneStrokeTracker> runeTrackers =
            new Dictionary<Handedness, RuneStrokeTracker>();
        private readonly Dictionary<Handedness, TimedHandTrail> palmTrails =
            new Dictionary<Handedness, TimedHandTrail>();
        private readonly Dictionary<Handedness, Vector3> trailAnchorTargets =
            new Dictionary<Handedness, Vector3>();
        private readonly Dictionary<Handedness, Vector3> trailAnchors =
            new Dictionary<Handedness, Vector3>();
        private readonly Dictionary<Handedness, bool> trailAnchorFromTip =
            new Dictionary<Handedness, bool>();
        private readonly HashSet<Handedness> seenRuneHands = new HashSet<Handedness>();
        private CameraManager cameraManager;
        private VisionPipeline visionPipeline;
        private FpsCounter displayFps;
        private SwordFingerDetector swordFingerDetector;
        private OverlaySettings overlaySettings;
        private bool settingsOpen;
        private float appliedHoverSeconds = -1f;
        private readonly Dictionary<Handedness, Vector3[]> renderedLandmarks =
            new Dictionary<Handedness, Vector3[]>();
        private GUIStyle settingsButtonStyle;
        private GUIStyle runeBuildMarkerStyle;
        private RuneFollowView runeFollowView;
        private RuneCastController runeCastController;
        private readonly Dictionary<Handedness, bool> trailFollowsTip =
            new Dictionary<Handedness, bool>();
        private readonly Dictionary<Handedness, int> pointingStreak =
            new Dictionary<Handedness, int>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private readonly Dictionary<Handedness, bool> lastLoggedPointing =
            new Dictionary<Handedness, bool>();
        private Handedness lastLoggedHandedness = Handedness.Unknown;
        private double nextTrailLogRealtime;
        private const double TrailLogIntervalSeconds = 2.0;
#endif
        private double latestResultTimestamp = double.NegativeInfinity;
        private double lastResultRealtime = double.NegativeInfinity;
        private double lastHandRealtime = double.NegativeInfinity;
        private double previousResultRealtime = double.NegativeInfinity;
        private float resultFps;
        private int detectedHands;
        private Texture2D whiteTexture;
        private GUIStyle statusStyle;
        private int currentRotation;
        private bool currentVerticallyMirrored;

        // True while this view draws the full-screen camera picture, so other
        // HUDs can hide their own overlapping preview.
        public bool IsDrawingCamera { get; private set; }

        // True whenever the full screen overlay is shown, whether or not the
        // camera picture itself is visible, so other HUDs can stay hidden in
        // both cases instead of only while the camera is drawn.
        public bool IsOverlayVisible => visible;

        // DebugHud uses the same switch so the settings panel controls all
        // diagnostic text, not only the small status line drawn here.
        public bool IsStatusTextVisible => overlaySettings == null || overlaySettings.ShowStatusText;

        private void Start()
        {
            visible = Debug.isDebugBuild || Application.isEditor;
            cameraManager = FindFirstObjectByType<CameraManager>();
            visionPipeline = FindFirstObjectByType<VisionPipeline>();
            runeFollowView = FindFirstObjectByType<RuneFollowView>();
            runeCastController = FindFirstObjectByType<RuneCastController>();
            displayFps = new FpsCounter();
            swordFingerDetector = new SwordFingerDetector();
            overlaySettings = new OverlaySettings();
            overlaySettings.Load();
            // The overlay honours the frame rate chosen in its own panel, so the
            // config value is only a starting point.

            PlayerPrefs.DeleteKey("mm.dbg.rot");
            PlayerPrefs.DeleteKey("mm.dbg.fx");
            PlayerPrefs.DeleteKey("mm.dbg.fy");
            GameManager.Instance?.Events.Subscribe<HandTrackingResult>(HandleResult);
        }

        private void Update()
        {
            displayFps.AddFrame(Time.unscaledDeltaTime);
            UpdateRenderedLandmarks();
            if (!Mathf.Approximately(appliedHoverSeconds, overlaySettings.HoverSeconds))
            {
                // The trackers keep their timing in readonly fields, so a change
                // of the hover setting rebuilds them instead of mutating.
                appliedHoverSeconds = overlaySettings.HoverSeconds;
                runeTrackers.Clear();
            }

            if (visionPipeline != null)
            {
                visionPipeline.TrackingRate = overlaySettings.TrackingRate;
            }

            // Throttling to a value that is not a whole fraction of the display
            // refresh rate makes the pacing uneven, which reads as skipped
            // frames, so the top setting hands pacing back to the display and
            // only the lower ones are throttled.
            var wantedFrameRate = overlaySettings.TargetFrameRate;
            var wantedVSync = wantedFrameRate >= 60 ? 1 : 0;
            var wantedTarget = wantedFrameRate >= 60 ? -1 : wantedFrameRate;
            if (QualitySettings.vSyncCount != wantedVSync)
            {
                QualitySettings.vSyncCount = wantedVSync;
            }

            if (Application.targetFrameRate != wantedTarget)
            {
                Application.targetFrameRate = wantedTarget;
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                visible = !visible;
            }
        }

        private void HandleResult(HandTrackingResult result)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (previousResultRealtime > 0d)
            {
                var interval = now - previousResultRealtime;
                if (interval > 0d)
                {
                    var instantaneous = (float)(1d / interval);
                    resultFps = resultFps <= 0f ? instantaneous : Mathf.Lerp(resultFps, instantaneous, 0.2f);
                }
            }

            previousResultRealtime = now;
            lastResultRealtime = now;
            latestResultTimestamp = result.Timestamp;
            detectedHands = 0;
            seenRuneHands.Clear();
            if (result.Hands.Count > 0)
            {
                displayedHands.Clear();
                for (var index = 0; index < result.Hands.Count; index++)
                {
                    var hand = result.Hands[index];
                    if (hand != null && hand.IsValid)
                    {
                        displayedHands.Add(hand);
                        detectedHands++;
                        UpdatePalmTrail(hand, now, result.WidthToHeight);
                        UpdateRuneTracker(hand, now, result.WidthToHeight);
                    }
                }

                if (displayedHands.Count > 0)
                {
                    lastHandRealtime = now;
                }
            }

            foreach (var pair in runeTrackers)
            {
                if (!seenRuneHands.Contains(pair.Key))
                {
                    pair.Value.UpdatePose(false, Vector3.zero, 1f, now);
                }
            }
        }

        private void UpdatePalmTrail(HandData hand, double timestamp, float widthToHeight)
        {
            if (hand.Handedness == Handedness.Unknown)
            {
                return;
            }

            if (!palmTrails.TryGetValue(hand.Handedness, out var trail))
            {
                trail = new TimedHandTrail();
                palmTrails.Add(hand.Handedness, trail);
            }

            // The same trail follows the index fingertip once the hand points
            // with a finger and the palm centre otherwise, so both cases keep
            // identical timing and look. The fingertip wins as soon as the
            // hand points with one or two fingers, and the palm is only a
            // fallback, so it takes six agreeing frames with no finger to
            // give up. Old points are kept and marked as a break so the trail
            // never draws a jump between the two anchors.
            var pointing = swordFingerDetector.IsPointing(hand, widthToHeight);
            var streak = 0;
            pointingStreak.TryGetValue(hand.Handedness, out streak);
            streak = Mathf.Clamp(streak + (pointing ? 1 : -1), -6, 6);
            pointingStreak[hand.Handedness] = streak;

            var followsTip = false;
            trailFollowsTip.TryGetValue(hand.Handedness, out followsTip);
            if (streak >= 1)
            {
                if (!followsTip)
                {
                    trail.MarkBreak();
                }

                followsTip = true;
            }
            else
            {
                if (streak <= -6)
                {
                    if (followsTip)
                    {
                        trail.MarkBreak();
                    }

                    followsTip = false;
                }
            }

            trailFollowsTip[hand.Handedness] = followsTip;
            var anchor = followsTip
                ? SwordFingerDetector.CalculateBrushPoint(hand)
                : HandGeometry.CalculatePalmCenter(hand);
            trail.LifetimeSeconds = overlaySettings.TrailLifetime;
            trail.Add(anchor, timestamp, followsTip);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            LogTrailAnchor(hand, widthToHeight, pointing, followsTip, anchor);
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // A trail that jumps can come from three places: the pointing decision
        // flipping, the handedness flipping so the trail of the other hand gets
        // drawn instead, or the fingertip landmark itself being unstable. Only
        // the flips go to logcat, plus a slow sample, which is what tells the
        // three apart without costing frames. Diagnostic only, so a release
        // build keeps none of it.
        private void LogTrailAnchor(
            HandData hand, float widthToHeight, bool pointing, bool followsTip, Vector3 anchor)
        {
            var previousPointing = false;
            var seen = lastLoggedPointing.TryGetValue(hand.Handedness, out previousPointing);
            var pointingFlipped = seen && previousPointing != pointing;
            lastLoggedPointing[hand.Handedness] = pointing;

            var handednessFlipped = lastLoggedHandedness != Handedness.Unknown &&
                lastLoggedHandedness != hand.Handedness;
            lastLoggedHandedness = hand.Handedness;

            var now = Time.realtimeSinceStartupAsDouble;
            var sampled = now >= nextTrailLogRealtime;
            if (!pointingFlipped && !handednessFlipped && !sampled)
            {
                return;
            }

            if (sampled)
            {
                nextTrailLogRealtime = now + TrailLogIntervalSeconds;
            }

            var fingers = swordFingerDetector.Analyze(hand, widthToHeight);
            var tip = SwordFingerDetector.CalculateBrushPoint(hand);
            Debug.Log(
                $"[MagicMotion.Trail] {(pointingFlipped ? "pointing flip" : handednessFlipped ? "handedness flip" : "sample")} " +
                $"rot={currentRotation} hand={hand.Handedness} pointing={pointing} followsTip={followsTip} " +
                $"thumb={fingers.Thumb} index={fingers.Index} middle={fingers.Middle} " +
                $"ring={fingers.Ring} pinky={fingers.Pinky} " +
                $"tip={tip.x:0.0000}/{tip.y:0.0000}/{tip.z:0.0000} " +
                $"anchor={anchor.x:0.0000}/{anchor.y:0.0000}");
        }
#endif

        private void SampleTrails(double now)
        {
            if (displayedHands.Count == 0)
            {
                trailAnchorTargets.Clear();
                trailAnchors.Clear();
                trailAnchorFromTip.Clear();
                return;
            }

            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / TrailFollowSeconds);
            for (var handIndex = 0; handIndex < displayedHands.Count; handIndex++)
            {
                var hand = displayedHands[handIndex];
                if (hand.Handedness == Handedness.Unknown)
                {
                    continue;
                }

                if (!trailAnchorTargets.TryGetValue(hand.Handedness, out var target))
                {
                    continue;
                }

                if (!palmTrails.TryGetValue(hand.Handedness, out var trail))
                {
                    continue;
                }

                var current = target;
                if (trailAnchors.TryGetValue(hand.Handedness, out var eased))
                {
                    current = Vector3.Lerp(eased, target, blend);
                }

                trailAnchors[hand.Handedness] = current;
                trail.LifetimeSeconds = overlaySettings.TrailLifetime;

                var fromTip = false;
                trailAnchorFromTip.TryGetValue(hand.Handedness, out fromTip);
                trail.Add(current, now, fromTip);
            }
        }

        private void UpdateRuneTracker(HandData hand, double timestamp, float widthToHeight)
        {
            if (hand.Handedness == Handedness.Unknown)
            {
                return;
            }

            seenRuneHands.Add(hand.Handedness);
            if (!runeTrackers.TryGetValue(hand.Handedness, out var tracker))
            {
                tracker = new RuneStrokeTracker(overlaySettings.HoverSeconds);
                runeTrackers.Add(hand.Handedness, tracker);
            }

            var isSwordFinger = swordFingerDetector.IsPointing(hand, widthToHeight);
            var brushPoint = isSwordFinger ? SwordFingerDetector.CalculateBrushPoint(hand) : Vector3.zero;
            tracker.UpdatePose(isSwordFinger, brushPoint, HandGeometry.CalculatePalmWidth(hand), timestamp);
        }

        private void OnGUI()
        {
            if (!visible || cameraManager == null)
            {
                return;
            }

            IsDrawingCamera = false;
            var texture = cameraManager.Texture;
            var safe = Screen.safeArea;
            var viewport = new Rect(safe.xMin, Screen.height - safe.yMax, safe.width, safe.height);
            GUI.color = Color.black;
            GUI.DrawTexture(viewport, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (texture != null && texture.width > 16 && texture.height > 16)
            {
                var rotation = HandTrackingFrameTransform.NormalizeRotation(texture.videoRotationAngle);
                currentRotation = rotation;
                currentVerticallyMirrored = texture.videoVerticallyMirrored;
                var content = HandTrackingOverlayLayout.Fit(viewport, texture.width, texture.height, rotation);
                if (overlaySettings.ShowCamera)
                {
                    DrawCorrectedCamera(texture, content, rotation, texture.videoVerticallyMirrored,
                        cameraManager.IsFrontFacing);
                    IsDrawingCamera = true;
                }
                else
                {
                    GUI.color = overlaySettings.BackgroundColor;
                    GUI.DrawTexture(content, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IsDrawingCamera = false;
                }
                DrawPalmTrails(content);
                if (overlaySettings.ShowSkeleton)
                {
                    DrawSkeletons(content);
                }
            }

            if (settingsOpen)
            {
                if (overlaySettings.DrawPanel(viewport))
                {
                    settingsOpen = false;
                }
            }
            else
            {
                DrawSettingsButton(viewport);
            }

            if (overlaySettings.ShowStatusText)
            {
                DrawStatus(viewport);
            }
        }

        private void DrawPalmTrails(Rect content)
        {
            if (!overlaySettings.ShowTrail)
            {
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            foreach (var pair in palmTrails)
            {
                var trail = pair.Value;
                trail.Prune(now);
                var points = trail.Points;
                for (var index = 1; index < points.Count; index++)
                {
                    if (points[index].BreakBefore)
                    {
                        continue;
                    }

                    var alpha = trail.Alpha(points[index], now);
                    var tone = overlaySettings.TrailColor(points[index].FromTip);
                    var color = new Color(tone.r, tone.g, tone.b, alpha * 0.8f);
                    var thickness = Mathf.Max(3f, Screen.dpi / 70f) *
                        overlaySettings.TrailThickness * Mathf.Lerp(0.5f, 1f, alpha);
                    var from = MapPoint(content, points[index - 1].Position);
                    var to = MapPoint(content, points[index].Position);

                    var steps = Mathf.Clamp(
                        Mathf.RoundToInt(Vector2.Distance(from, to) / CurveSegmentPixels),
                        1, MaximumCurveSegments);
                    if (steps == 1)
                    {
                        DrawLine(from, to, color, thickness);
                        continue;
                    }

                    // The spline wants a sample either side of the span. Where
                    // that neighbour is on the far side of a break, or there
                    // isn't one, the missing end is mirrored instead of reused:
                    // reusing it would leave a zero length knot and flatten the
                    // curve into a straight line again.
                    var before = index >= 2 && !points[index - 1].BreakBefore
                        ? MapPoint(content, points[index - 2].Position)
                        : from + (from - to);
                    var after = index + 1 < points.Count && !points[index + 1].BreakBefore
                        ? MapPoint(content, points[index + 1].Position)
                        : to + (to - from);

                    var start = from;
                    for (var step = 1; step <= steps; step++)
                    {
                        var end = step == steps
                            ? to
                            : TrailCurve.Sample(before, from, to, after, (float)step / steps);
                        DrawLine(start, end, color, thickness);
                        start = end;
                    }
                }
            }
        }

        private bool TryGetDisplayedHand(Handedness handedness, out HandData hand)
        {
            for (var index = 0; index < displayedHands.Count; index++)
            {
                if (displayedHands[index].Handedness == handedness)
                {
                    hand = displayedHands[index];
                    return true;
                }
            }

            hand = null;
            return false;
        }

        private void DrawCorrectedCamera(
            Texture texture,
            Rect content,
            int rotation,
            bool flipVertically,
            bool flipHorizontally)
        {
            var swapped = rotation == 90 || rotation == 270;
            var unrotated = swapped
                ? new Rect(content.center.x - content.height * 0.5f, content.center.y - content.width * 0.5f,
                    content.height, content.width)
                : content;
            var uv = HandTrackingFrameTransform.GetDisplayUv(
                flipHorizontally, flipVertically, rotation);
            var previous = GUI.matrix;
            var pivot = content.center;
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, -rotation), Vector3.one) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            GUI.DrawTextureWithTexCoords(unrotated, texture, uv, true);
            GUI.matrix = previous;
        }

        /// <summary>
        /// Detections arrive at roughly the tracking rate while the view draws at
        /// the display rate, so raw landmarks jump in steps. Easing the rendered
        /// positions towards the latest result every drawn frame turns those
        /// steps into continuous motion without waiting for the next detection.
        /// </summary>
        private const float RenderSmoothingSeconds = 0.025f;

        // How fast the drawn trail anchor closes on the latest detection. It
        // is kept short so a quick wave is not smeared into a slow curve.
        private const float TrailFollowSeconds = 0.025f;

        // A quicker stroke means the samples land further apart, so each span is
        // drawn as a curve cut into pieces about this long. Short spans stay a
        // single straight piece, so the cost is only paid where the stroke is
        // quick enough to show folds, and the cap keeps a wild swing from
        // turning into hundreds of draws.
        private const float CurveSegmentPixels = 8f;
        private const int MaximumCurveSegments = 12;

        private void UpdateRenderedLandmarks()
        {
            foreach (var pair in renderedLandmarks)
            {
                var stillPresent = false;
                for (var index = 0; index < displayedHands.Count; index++)
                {
                    if (displayedHands[index].Handedness == pair.Key)
                    {
                        stillPresent = true;
                        break;
                    }
                }

                if (!stillPresent)
                {
                    renderedLandmarks.Remove(pair.Key);
                    break;
                }
            }

            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / RenderSmoothingSeconds);
            for (var handIndex = 0; handIndex < displayedHands.Count; handIndex++)
            {
                var hand = displayedHands[handIndex];
                if (!renderedLandmarks.TryGetValue(hand.Handedness, out var positions))
                {
                    positions = new Vector3[HandData.RequiredLandmarkCount];
                    for (var index = 0; index < positions.Length; index++)
                    {
                        positions[index] = hand.Landmarks[index].Position;
                    }

                    renderedLandmarks.Add(hand.Handedness, positions);
                    continue;
                }

                for (var index = 0; index < positions.Length; index++)
                {
                    positions[index] = Vector3.Lerp(
                        positions[index], hand.Landmarks[index].Position, blend);
                }
            }
        }

        private Vector3 GetRenderedPosition(HandData hand, int landmarkIndex)
        {
            if (renderedLandmarks.TryGetValue(hand.Handedness, out var positions))
            {
                return positions[landmarkIndex];
            }

            return hand.Landmarks[landmarkIndex].Position;
        }
        private void DrawSkeletons(Rect content)
        {
            var age = Time.realtimeSinceStartupAsDouble - lastHandRealtime;
            var alpha = age <= LostHoldSeconds
                ? 1f
                : 1f - Mathf.Clamp01((float)((age - LostHoldSeconds) / LostFadeSeconds));
            if (alpha <= 0f)
            {
                displayedHands.Clear();
                return;
            }

            for (var handIndex = 0; handIndex < displayedHands.Count; handIndex++)
            {
                var hand = displayedHands[handIndex];
                var color = hand.Handedness == Handedness.Left
                    ? new Color(0.2f, 0.85f, 1f, alpha)
                    : new Color(1f, 0.75f, 0.15f, alpha);
                for (var pair = 0; pair < BonePairs.Length; pair += 2)
                {
                    var from = MapPoint(
                        content, GetRenderedPosition(hand, BonePairs[pair]));
                    var to = MapPoint(
                        content, GetRenderedPosition(hand, BonePairs[pair + 1]));
                    DrawLine(from, to, color, Mathf.Max(3f, Screen.dpi / 80f));
                }

                for (var landmarkIndex = 0; landmarkIndex < HandData.RequiredLandmarkCount; landmarkIndex++)
                {
                    var point = MapPoint(
                        content, GetRenderedPosition(hand, landmarkIndex));
                    DrawPoint(point, color, Mathf.Max(8f, Screen.dpi / 20f));
                }
            }
        }

        private Vector2 MapPoint(Rect content, Vector3 normalized)
        {
            var point = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                normalized, currentRotation, cameraManager.IsFrontFacing, currentVerticallyMirrored);
            point.x = content.xMin + Mathf.Clamp01(point.x) * content.width;
            point.y = content.yMin + Mathf.Clamp01(point.y) * content.height;
            return point;
        }
        private void DrawSettingsButton(Rect viewport)
        {
            // 48dp is the smallest comfortable touch target on Android. The
            // style is built once and its glyphs are warmed up right away,
            // because a dynamic font renders nothing until its characters have
            // been rasterized, which used to leave the button blank at start.
            if (settingsButtonStyle == null)
            {
                var scale = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 2f, 1f, 3f);
                var font = Resources.Load<Font>("Fonts/SimHei");
                settingsButtonStyle = new GUIStyle(GUI.skin.button);
                settingsButtonStyle.fontSize = (int)(16f * scale);
                settingsButtonStyle.normal.textColor = Color.white;
                if (font != null)
                {
                    settingsButtonStyle.font = font;
                    font.RequestCharactersInTexture("设置", settingsButtonStyle.fontSize, FontStyle.Normal);
                }
                else
                {
                    Debug.LogWarning("[MagicMotion.UI] Fonts/SimHei is missing, the settings button falls back to the default font.");
                }
            }

            var buttonScale = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 2f, 1f, 3f);
            var width = 112f * buttonScale;
            var height = 48f * buttonScale;
            var rect = new Rect(
                viewport.xMax - width - 12f * buttonScale,
                viewport.y + 12f * buttonScale,
                width,
                height);
            if (GUI.Button(rect, "设置", settingsButtonStyle))
            {
                settingsOpen = true;
            }

            DrawRuneBuildMarker(rect, buttonScale);
        }

        private void DrawRuneBuildMarker(Rect settingsRect, float scale)
        {
            if (runeFollowView == null)
            {
                runeFollowView = FindFirstObjectByType<RuneFollowView>();
            }

            if (runeCastController == null)
            {
                runeCastController = FindFirstObjectByType<RuneCastController>();
            }

            if (runeBuildMarkerStyle == null)
            {
                runeBuildMarkerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperRight,
                    fontSize = Mathf.RoundToInt(11f * scale),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.83f, 0.26f) }
                };
            }

            var state = runeCastController == null
                ? "component=missing"
                : $"events={runeCastController.ReceivedResultCount} " +
                  $"hands={runeCastController.ValidHandsInLatestResult} " +
                  $"visible={(runeFollowView != null && runeFollowView.IsRuneVisible ? 1 : 0)} " +
                  $"anchor={(runeFollowView != null && runeFollowView.HasRenderAnchor ? 1 : 0)}\n" +
                  $"draw={(runeFollowView == null ? "no-view" : runeFollowView.DrawState)}";
            var markerRect = new Rect(
                settingsRect.x - 190f * scale,
                settingsRect.yMax + 5f * scale,
                settingsRect.width + 190f * scale,
                56f * scale);
            GUI.Label(markerRect, $"{RuneFollowView.BuildMarker}\n{state}", runeBuildMarkerStyle);
        }

        private void DrawStatus(Rect viewport)
        {
            EnsureResources();
            var ageMilliseconds = lastResultRealtime > 0d
                ? (Time.realtimeSinceStartupAsDouble - lastResultRealtime) * 1000d
                : -1d;
            var status =
                $"HAND TRACKING DEBUG  F4: hide\n" +
                $"Camera: {cameraManager.State}  Vision: {visionPipeline?.Status.ToString() ?? "Missing"}\n" +
                $"Hands: {detectedHands}  Display: {displayFps.Current:0.0} FPS  Tracking: {resultFps:0.0} FPS\n" +
                $"Rot: {currentRotation} MirrorV: {(currentVerticallyMirrored ? 1 : 0)}  Rune: {DescribeRuneState()}  " +
                $"Result age: {(ageMilliseconds < 0d ? "-" : ageMilliseconds.ToString("0") + " ms")}  " +
                $"Media time: {(double.IsNegativeInfinity(latestResultTimestamp) ? "-" : latestResultTimestamp.ToString("0.000") + " s")}";
            var scale = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1f, 1f, 2f);
            var rect = new Rect(viewport.x + 12f, viewport.y + 12f,
                Mathf.Min(viewport.width - 24f, 620f * scale), 112f * scale);
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 16f), status, statusStyle);
        }

        private string DescribeRuneState()
        {
            foreach (var pair in runeTrackers)
            {
                if (pair.Value.State == RuneStrokeState.Drawing)
                {
                    return $"{pair.Key} Drawing";
                }

                if (pair.Value.State == RuneStrokeState.SwordFingerCandidate)
                {
                    return $"{pair.Key} Hold {pair.Value.CandidateProgress * 100f:0}%";
                }
            }

            return "Idle";
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            EnsureResources();
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var previous = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), whiteTexture);
            GUI.matrix = previous;
            GUI.color = Color.white;
        }

        private void DrawPoint(Vector2 center, Color color, float diameter)
        {
            EnsureResources();
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter),
                whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureResources()
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(16f * Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1f, 1f, 2f)),
                    normal = { textColor = Color.white },
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void OnDestroy()
        {
            GameManager.Instance?.Events.Unsubscribe<HandTrackingResult>(HandleResult);
            if (whiteTexture != null)
            {
                Destroy(whiteTexture);
            }
        }
    }
}
