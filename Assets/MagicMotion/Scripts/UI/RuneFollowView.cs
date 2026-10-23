using MagicMotion.Camera;
using MagicMotion.Core;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.UI
{
    [DisallowMultipleComponent]
    public sealed class RuneFollowView : MonoBehaviour
    {
        public const string BuildMarker = "RF-CHECK-B1";
        private const float FollowSeconds = 0.035f;
        private const float LostResultSeconds = 0.35f;

        private CameraManager cameraManager;
        private RuneFollowTracker tracker;
        private Vector3 renderedTarget;
        private bool hasRenderedTarget;
        private double lastResultAt = double.NegativeInfinity;
        private Texture2D whiteTexture;

        public int ReceivedResultCount { get; private set; }
        public int ValidHandsInLatestResult { get; private set; }
        public bool IsRuneVisible => tracker != null && tracker.Visible;
        public bool HasRenderAnchor => hasRenderedTarget;

        // Diagnostic only: why OnGUI last returned early, or where the rune
        // was drawn. Surfaced next to the build marker so a future "no rune"
        // report can be triaged from a single screenshot.
        public string DrawState { get; private set; } = "pending";

        private void Start()
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
            tracker = new RuneFollowTracker();
            GameManager.Instance?.Events.Subscribe<HandTrackingResult>(HandleResult);
        }

        private void HandleResult(HandTrackingResult result)
        {
            ReceivedResultCount++;
            lastResultAt = Time.realtimeSinceStartupAsDouble;
            // This first classroom checkpoint proves only the visual follow
            // chain. If the hand skeleton is on screen, the base rune must be
            // on screen too; pose and spell-state gates are added only after
            // this has been accepted on device.
            HandData best = null;
            ValidHandsInLatestResult = 0;
            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                if (hand == null || !hand.IsValid)
                {
                    continue;
                }

                ValidHandsInLatestResult++;

                if (best == null || hand.Confidence > best.Confidence)
                {
                    best = hand;
                }
            }

            var now = Time.realtimeSinceStartupAsDouble;
            if (best == null)
            {
                tracker.UpdatePose(false, Vector3.zero, now);
                return;
            }

            tracker.UpdatePose(true, RuneFollowTracker.CalculateTarget(best), now);
        }

        private void Update()
        {
            if (tracker == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            if (now - lastResultAt > LostResultSeconds)
            {
                tracker.Reset();
                hasRenderedTarget = false;
                return;
            }

            if (!tracker.Visible)
            {
                hasRenderedTarget = false;
                return;
            }

            if (!hasRenderedTarget)
            {
                renderedTarget = tracker.Target;
                hasRenderedTarget = true;
                return;
            }

            var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / FollowSeconds);
            renderedTarget = Vector3.Lerp(renderedTarget, tracker.Target, blend);
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
            if (tracker == null || cameraManager == null)
            {
                DrawState = tracker == null ? "no-tracker" : "no-camera";
                return;
            }

            if (!hasRenderedTarget)
            {
                DrawState = "no-anchor";
                return;
            }

            if (!tracker.Visible)
            {
                DrawState = "not-visible";
                return;
            }

            var texture = cameraManager.Texture;
            if (texture == null || texture.width <= 16 || texture.height <= 16)
            {
                DrawState = "no-texture";
                return;
            }

            EnsureTexture();
            var safe = Screen.safeArea;
            var viewport = new Rect(safe.xMin, Screen.height - safe.yMax, safe.width, safe.height);
            var rotation = HandTrackingFrameTransform.NormalizeRotation(texture.videoRotationAngle);
            var content = HandTrackingOverlayLayout.Fit(viewport, texture.width, texture.height, rotation);
            var normalized = HandTrackingFrameTransform.LandmarkToGuiNormalized(
                renderedTarget, rotation, cameraManager.IsFrontFacing, texture.videoVerticallyMirrored);
            var center = new Vector2(
                content.xMin + Mathf.Clamp01(normalized.x) * content.width,
                content.yMin + Mathf.Clamp01(normalized.y) * content.height);

            var scale = Mathf.Clamp(Mathf.Min(content.width, content.height) / 720f, 0.65f, 1.8f);
            DrawState = $"drew {center.x:F0}/{center.y:F0} s{scale:F2}";

            DrawBlankRune(center, scale);
        }

        private void DrawBlankRune(Vector2 center, float scale)
        {
            // GUI.depth is assigned at the top of OnGUI (-99), so the rune
            // lands in front of the camera overlay while the settings panel
            // stays interactive.
            var bob = Mathf.Sin(Time.unscaledTime * 2.4f) * 3f * scale;
            center.y += bob;
            var glow = new Rect(center.x - 39f * scale, center.y - 68f * scale, 78f * scale, 136f * scale);
            var paper = new Rect(center.x - 28f * scale, center.y - 58f * scale, 56f * scale, 116f * scale);

            GUI.color = new Color(1f, 0.72f, 0.16f, 0.16f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f));
            GUI.DrawTexture(glow, whiteTexture);
            GUI.color = new Color(0.92f, 0.76f, 0.38f, 0.94f);
            GUI.DrawTexture(paper, whiteTexture);

            var ink = new Color(0.38f, 0.12f, 0.035f, 0.82f);
            DrawBar(new Rect(center.x - 17f * scale, center.y - 40f * scale, 34f * scale, 3f * scale), ink);
            DrawBar(new Rect(center.x - 3f * scale, center.y - 36f * scale, 6f * scale, 50f * scale), ink);
            DrawBar(new Rect(center.x - 14f * scale, center.y - 10f * scale, 28f * scale, 3f * scale), ink);
            DrawBar(new Rect(center.x - 16f * scale, center.y + 17f * scale, 32f * scale, 3f * scale), ink);
            DrawBar(new Rect(center.x - 12f * scale, center.y + 30f * scale, 24f * scale, 3f * scale), ink);

            GUI.color = Color.white;
        }

        private void DrawBar(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
        }

        private void EnsureTexture()
        {
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
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
