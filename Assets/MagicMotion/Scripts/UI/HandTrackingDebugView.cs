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

        private const float LostHoldSeconds = 0.18f;
        private const float LostFadeSeconds = 0.22f;

        [SerializeField] private bool visible = true;
        private readonly List<HandData> displayedHands = new List<HandData>(2);
        private readonly Dictionary<Handedness, RuneStrokeTracker> runeTrackers =
            new Dictionary<Handedness, RuneStrokeTracker>();
        private readonly Dictionary<Handedness, TimedHandTrail> palmTrails =
            new Dictionary<Handedness, TimedHandTrail>();
        private readonly HashSet<Handedness> seenRuneHands = new HashSet<Handedness>();
        private CameraManager cameraManager;
        private VisionPipeline visionPipeline;
        private FpsCounter displayFps;
        private SwordFingerDetector swordFingerDetector;
        private double latestResultTimestamp = double.NegativeInfinity;
        private double lastResultRealtime = double.NegativeInfinity;
        private double lastHandRealtime = double.NegativeInfinity;
        private double previousResultRealtime = double.NegativeInfinity;
        private float resultFps;
        private int detectedHands;
        private Texture2D whiteTexture;
        private GUIStyle statusStyle;
        private int currentRotation;

        private void Start()
        {
            visible = Debug.isDebugBuild || Application.isEditor;
            cameraManager = FindFirstObjectByType<CameraManager>();
            visionPipeline = FindFirstObjectByType<VisionPipeline>();
            displayFps = new FpsCounter();
            swordFingerDetector = new SwordFingerDetector();
            GameManager.Instance?.Events.Subscribe<HandTrackingResult>(HandleResult);
        }

        private void Update()
        {
            displayFps.AddFrame(Time.unscaledDeltaTime);
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
                        UpdatePalmTrail(hand, now);
                        UpdateRuneTracker(hand, now);
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

        private void UpdatePalmTrail(HandData hand, double timestamp)
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

            trail.Add(HandGeometry.CalculatePalmCenter(hand), timestamp);
        }

        private void UpdateRuneTracker(HandData hand, double timestamp)
        {
            if (hand.Handedness == Handedness.Unknown)
            {
                return;
            }

            seenRuneHands.Add(hand.Handedness);
            if (!runeTrackers.TryGetValue(hand.Handedness, out var tracker))
            {
                tracker = new RuneStrokeTracker();
                runeTrackers.Add(hand.Handedness, tracker);
            }

            var isSwordFinger = swordFingerDetector.IsSwordFinger(hand);
            var brushPoint = isSwordFinger ? SwordFingerDetector.CalculateBrushPoint(hand) : Vector3.zero;
            tracker.UpdatePose(isSwordFinger, brushPoint, HandGeometry.CalculatePalmWidth(hand), timestamp);
        }

        private void OnGUI()
        {
            if (!visible || cameraManager == null)
            {
                return;
            }

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
                var content = HandTrackingOverlayLayout.Fit(viewport, texture.width, texture.height, rotation);
                DrawCorrectedCamera(texture, content, rotation, texture.videoVerticallyMirrored,
                    cameraManager.IsFrontFacing);
                DrawPalmTrails(content);
                DrawSkeletons(content);
                DrawRuneStrokes(content);
            }

            DrawStatus(viewport);
        }

        private void DrawPalmTrails(Rect content)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            foreach (var pair in palmTrails)
            {
                var trail = pair.Value;
                trail.Prune(now);
                var points = trail.Points;
                for (var index = 1; index < points.Count; index++)
                {
                    var alpha = trail.Alpha(points[index], now);
                    var color = new Color(0.1f, 0.9f, 1f, alpha * 0.8f);
                    var from = MapPoint(content, points[index - 1].Position);
                    var to = MapPoint(content, points[index].Position);
                    DrawLine(from, to, color, Mathf.Max(3f, Screen.dpi / 70f) * Mathf.Lerp(0.5f, 1f, alpha));
                }
            }
        }

        private void DrawRuneStrokes(Rect content)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var gold = new Color(1f, 0.72f, 0.08f, 0.95f);
            foreach (var pair in runeTrackers)
            {
                var tracker = pair.Value;
                tracker.Tick(now);
                var points = tracker.Points;
                for (var index = 1; index < points.Count; index++)
                {
                    var from = MapPoint(content, points[index - 1]);
                    var to = MapPoint(content, points[index]);
                    DrawLine(from, to, gold, Mathf.Max(6f, Screen.dpi / 45f));
                }

                if (tracker.State == RuneStrokeState.SwordFingerCandidate &&
                    TryGetDisplayedHand(pair.Key, out var hand))
                {
                    var brush = MapPoint(content, SwordFingerDetector.CalculateBrushPoint(hand));
                    var diameter = Mathf.Lerp(12f, 28f, tracker.CandidateProgress);
                    DrawPoint(brush, new Color(gold.r, gold.g, gold.b, 0.45f + tracker.CandidateProgress * 0.5f),
                        Mathf.Max(diameter, Screen.dpi / 14f));
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
                    var from = MapPoint(content, hand.Landmarks[BonePairs[pair]].Position);
                    var to = MapPoint(content, hand.Landmarks[BonePairs[pair + 1]].Position);
                    DrawLine(from, to, color, Mathf.Max(3f, Screen.dpi / 80f));
                }

                for (var landmarkIndex = 0; landmarkIndex < HandData.RequiredLandmarkCount; landmarkIndex++)
                {
                    var point = MapPoint(content, hand.Landmarks[landmarkIndex].Position);
                    DrawPoint(point, color, Mathf.Max(8f, Screen.dpi / 20f));
                }
            }
        }

        private Vector2 MapPoint(Rect content, Vector3 normalized)
        {
            var point = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                normalized, currentRotation, cameraManager.IsFrontFacing);
            return new Vector2(
                content.xMin + Mathf.Clamp01(point.x) * content.width,
                content.yMin + Mathf.Clamp01(point.y) * content.height);
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
                $"Rune: {DescribeRuneState()}  Result age: {(ageMilliseconds < 0d ? "-" : ageMilliseconds.ToString("0") + " ms")}  " +
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
