using System.Collections.Generic;
using MagicMotion.Camera;
using MagicMotion.Core;
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
        private CameraManager cameraManager;
        private VisionPipeline visionPipeline;
        private FpsCounter displayFps;
        private double latestResultTimestamp = double.NegativeInfinity;
        private double lastResultRealtime = double.NegativeInfinity;
        private double lastHandRealtime = double.NegativeInfinity;
        private double previousResultRealtime = double.NegativeInfinity;
        private float resultFps;
        private Texture2D whiteTexture;
        private GUIStyle statusStyle;

        private void Start()
        {
            visible = Debug.isDebugBuild || Application.isEditor;
            cameraManager = FindFirstObjectByType<CameraManager>();
            visionPipeline = FindFirstObjectByType<VisionPipeline>();
            displayFps = new FpsCounter();
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
            if (result.Hands.Count > 0)
            {
                displayedHands.Clear();
                for (var index = 0; index < result.Hands.Count; index++)
                {
                    var hand = result.Hands[index];
                    if (hand != null && hand.IsValid)
                    {
                        displayedHands.Add(hand);
                    }
                }

                if (displayedHands.Count > 0)
                {
                    lastHandRealtime = now;
                }
            }
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
                var rotation = HandTrackingOverlayLayout.NormalizeRotation(texture.videoRotationAngle);
                var content = HandTrackingOverlayLayout.Fit(viewport, texture.width, texture.height, rotation);
                DrawCorrectedCamera(texture, content, rotation, texture.videoVerticallyMirrored,
                    cameraManager.IsFrontFacing);
                DrawSkeletons(content);
            }

            DrawStatus(viewport);
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
            var uv = new Rect(flipHorizontally ? 1f : 0f, flipVertically ? 1f : 0f,
                flipHorizontally ? -1f : 1f, flipVertically ? -1f : 1f);
            var previous = GUI.matrix;
            var pivot = content.center;
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, rotation), Vector3.one) *
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
                    var from = HandTrackingOverlayLayout.NormalizedToGui(
                        content, hand.Landmarks[BonePairs[pair]].Position);
                    var to = HandTrackingOverlayLayout.NormalizedToGui(
                        content, hand.Landmarks[BonePairs[pair + 1]].Position);
                    DrawLine(from, to, color, Mathf.Max(3f, Screen.dpi / 80f));
                }

                for (var landmarkIndex = 0; landmarkIndex < HandData.RequiredLandmarkCount; landmarkIndex++)
                {
                    var point = HandTrackingOverlayLayout.NormalizedToGui(
                        content, hand.Landmarks[landmarkIndex].Position);
                    DrawPoint(point, color, Mathf.Max(8f, Screen.dpi / 20f));
                }
            }
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
                $"Hands: {displayedHands.Count}  Display: {displayFps.Current:0.0} FPS  Tracking: {resultFps:0.0} FPS\n" +
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
