using System.Collections.Generic;
using System.Text;
using MagicMotion.Camera;
using MagicMotion.Combat;
using MagicMotion.Config;
using MagicMotion.Core;
using MagicMotion.Enemy;
using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Skill;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.UI
{
    [DisallowMultipleComponent]
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;

        private readonly Dictionary<Handedness, GestureType> gestures =
            new Dictionary<Handedness, GestureType>();
        private readonly Dictionary<Handedness, MotionType> motions =
            new Dictionary<Handedness, MotionType>();
        private readonly Dictionary<Handedness, string> skills =
            new Dictionary<Handedness, string>();
        private readonly Dictionary<Handedness, Vector3> palmNormals =
            new Dictionary<Handedness, Vector3>();
        private readonly Dictionary<Handedness, PalmDirection> palmDirections =
            new Dictionary<Handedness, PalmDirection>();
        private readonly Dictionary<Handedness, double> gestureTimestamps =
            new Dictionary<Handedness, double>();
        private readonly Dictionary<Handedness, double> lastForwardTimestamps =
            new Dictionary<Handedness, double>();
        private readonly Dictionary<Handedness, double> lastOpenPalmTimestamps =
            new Dictionary<Handedness, double>();

        // Mirrors SK001's inputMaxAge; used only to explain why a cast failed.
        private const float DiagnosticInputMaxAge = 0.25f;
        // Mirrors SkillManager.PalmMaxAge.
        private const double DiagnosticPalmMaxAge = 0.3d;
        // Mirrors SkillManager.GestureMemorySeconds.
        private const double DiagnosticGestureMemory = 1.0d;

        private int palmForwardFrames;
        private int openPalmEvents;
        private int pushEvents;
        private int castEvents;
        private string lastRejection = "-";

        private CameraManager cameraManager;
        private VisionPipeline visionPipeline;
        private MediaPipeHandTrackingProvider trackingProvider;
        private PalmDirectionClassifier palmClassifier;
        private FpsCounter fpsCounter;
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private string cameraMessage = "Not started";
        private string visionMessage = "Not started";
        private int detectedHands;
        private MotionConfig motionConfig;
        private float enemyHealth = -1f;
        private float enemyMaximumHealth = -1f;
        private string battleStatus = "Waiting";
        private EnemyHealth observedEnemy;

        private void Start()
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
            visionPipeline = FindFirstObjectByType<VisionPipeline>();
            motionConfig = FindFirstObjectByType<ConfigManager>()?.Motion ?? new MotionConfig();
            trackingProvider = FindFirstObjectByType<MediaPipeHandTrackingProvider>();
            palmClassifier = new PalmDirectionClassifier();
            fpsCounter = new FpsCounter();
            gestures[Handedness.Left] = GestureType.Unknown;
            gestures[Handedness.Right] = GestureType.Unknown;
            motions[Handedness.Left] = MotionType.Unknown;
            motions[Handedness.Right] = MotionType.Unknown;
            skills[Handedness.Left] = "Idle";
            skills[Handedness.Right] = "Idle";
            palmNormals[Handedness.Left] = Vector3.zero;
            palmNormals[Handedness.Right] = Vector3.zero;
            palmDirections[Handedness.Left] = PalmDirection.Unknown;
            palmDirections[Handedness.Right] = PalmDirection.Unknown;
            gestureTimestamps[Handedness.Left] = -1d;
            gestureTimestamps[Handedness.Right] = -1d;
            lastForwardTimestamps[Handedness.Left] = double.NegativeInfinity;
            lastForwardTimestamps[Handedness.Right] = double.NegativeInfinity;
            lastOpenPalmTimestamps[Handedness.Left] = double.NegativeInfinity;
            lastOpenPalmTimestamps[Handedness.Right] = double.NegativeInfinity;

            var events = GameManager.Instance.Events;
            events.Subscribe<CameraStatusChanged>(HandleCameraStatus);
            events.Subscribe<VisionStatusChanged>(HandleVisionStatus);
            events.Subscribe<HandTrackingResult>(HandleHands);
            events.Subscribe<GestureDetected>(HandleGesture);
            events.Subscribe<GestureLost>(HandleGestureLost);
            events.Subscribe<MotionDetected>(HandleMotion);
            events.Subscribe<SkillStateChanged>(HandleSkillState);
            events.Subscribe<DamageApplied>(HandleDamage);
            events.Subscribe<BattleCompleted>(HandleBattleCompleted);
        }

        private void Update()
        {
            fpsCounter.AddFrame(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.F3))
            {
                visible = !visible;
            }

            if (observedEnemy == null && GameManager.Instance.State == GameState.Battle)
            {
                observedEnemy = FindFirstObjectByType<EnemyHealth>();
            }

            if (observedEnemy != null)
            {
                enemyHealth = observedEnemy.CurrentHealth;
                enemyMaximumHealth = observedEnemy.MaximumHealth;
            }
        }

        private void OnGUI()
        {
            if (!visible || (!Debug.isDebugBuild && !Application.isEditor))
            {
                return;
            }

            EnsureStyles();
            var scale = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1f, 1f, 2f);
            var width = Mathf.Min(430f * scale, Screen.width - 24f);
            var height = Mathf.Min(580f * scale, Screen.height - 24f);
            var safe = Screen.safeArea;
            var topInset = Screen.height - safe.yMax;
            var rect = new Rect(safe.xMin + 12f, topInset + 12f, width, height);
            GUI.Box(rect, GUIContent.none, panelStyle);

            var previewHeight = Mathf.Min((rect.width - 28f) * 0.62f, rect.height * 0.42f);
            var previewRect = new Rect(
                rect.x + 14f,
                rect.yMax - previewHeight - 14f,
                rect.width - 28f,
                previewHeight);
            DrawCameraPreview(previewRect);

            var content = BuildText();
            var textRect = new Rect(
                rect.x + 14f,
                rect.y + 12f,
                rect.width - 28f,
                rect.height - previewHeight - 38f);
            GUI.Label(textRect, content, labelStyle);
        }

        private void DrawCameraPreview(Rect rect)
        {
            var texture = cameraManager != null ? cameraManager.Texture : null;
            if (texture == null || texture.width <= 16)
            {
                return;
            }

            var rotation = texture.videoRotationAngle;
            var swapped = rotation == 90 || rotation == 270;
            var drawRect = swapped
                ? new Rect(
                    rect.center.x - rect.height * 0.5f,
                    rect.center.y - rect.width * 0.5f,
                    rect.height,
                    rect.width)
                : rect;

            var previous = GUI.matrix;
            var pivot = new Vector2(rect.center.x, rect.center.y);
            // GUI's y axis points down, so a positive Euler angle rotates
            // clockwise on screen, matching videoRotationAngle's convention.
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, rotation), Vector3.one) *
                         Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleToFit);
            GUI.matrix = previous;
        }

        private string BuildText()
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("MAGIC MOTION / DEBUG");
            builder.Append("FPS: ").Append(fpsCounter.Current.ToString("0.0")).AppendLine();
            builder.Append("Game: ").Append(GameManager.Instance.State).AppendLine();
            builder.Append("Camera: ").Append(cameraManager?.State.ToString() ?? "Missing")
                .Append(" / ").Append(cameraMessage).AppendLine();
            builder.Append("Vision: ").Append(visionPipeline?.Status.ToString() ?? "Missing")
                .Append(" / ").Append(visionPipeline?.StatusMessage ?? visionMessage).AppendLine();
            builder.Append("Frames: recv=").Append(visionPipeline?.FramesReceived ?? 0)
                .Append(" proc=").Append(visionPipeline?.FramesProcessed ?? 0)
                .Append(" pub=").Append(visionPipeline?.ResultsPublished ?? 0).AppendLine();
            builder.Append("Input: ");
            if (trackingProvider != null)
            {
                builder.Append(trackingProvider.LastInputWidth).Append("x")
                    .Append(trackingProvider.LastInputHeight)
                    .Append(" luma=").Append(trackingProvider.LastFrameLuma.ToString("0.00"))
                    .Append(" handScale=").Append(trackingProvider.LastMaxPalmWidth.ToString("0.000"));
            }
            else
            {
                builder.Append("-");
            }
            builder.AppendLine();
            builder.Append("Hands: ").Append(detectedHands).AppendLine();
            AppendHand(builder, Handedness.Left);
            AppendHand(builder, Handedness.Right);
            builder.Append("Diag: palmFwd=").Append(palmForwardFrames)
                .Append(" openPalm=").Append(openPalmEvents)
                .Append(" push=").Append(pushEvents)
                .Append(" cast=").Append(castEvents).AppendLine();
            builder.Append("Last miss: ").Append(lastRejection).AppendLine();

            if (enemyMaximumHealth > 0f)
            {
                builder.Append("Enemy HP: ").Append(enemyHealth.ToString("0"))
                    .Append(" / ").Append(enemyMaximumHealth.ToString("0")).AppendLine();
            }

            builder.Append("Battle: ").Append(battleStatus).AppendLine();
            builder.Append("Hint: ").Append(BuildHint());
            return builder.ToString();
        }

        private void AppendHand(StringBuilder builder, Handedness hand)
        {
            builder.Append(hand).Append(": Gesture=").Append(gestures[hand])
                .Append(" Motion=").Append(motions[hand])
                .Append(" Skill=").Append(skills[hand]).AppendLine();

            var normal = palmNormals[hand];
            builder.Append("   palm=").Append(palmDirections[hand])
                .Append(" n=(").Append(normal.x.ToString("0.00"))
                .Append(",").Append(normal.y.ToString("0.00"))
                .Append(",").Append(normal.z.ToString("0.00")).Append(")")
                .AppendLine();
        }

        private string BuildHint()
        {
            if (cameraManager == null || cameraManager.State == CameraState.PermissionDenied)
            {
                return "Camera permission required";
            }

            if (cameraManager.State != CameraState.Running)
            {
                return cameraMessage;
            }

            if (visionPipeline == null || visionPipeline.Status == VisionStatus.WaitingForProvider)
            {
                return "Hand-tracking provider not installed";
            }

            if (detectedHands == 0)
            {
                return "Show an open palm to the camera";
            }

            // Growth shrinks with distance, and past roughly 0.8m a real push
            // produces the same paltry growth as everyday hand movement, so no
            // threshold can separate them there. Say so rather than leaving the
            // user pushing into a range that cannot work.
            var palmWidth = trackingProvider != null ? trackingProvider.LastMaxPalmWidth : 0f;
            if (palmWidth > 0f && palmWidth < motionConfig.nearEnoughPalmWidth)
            {
                return "Too far: move closer to the camera";
            }

            return "Palm facing camera, then push forward fast";
        }

        private void HandleCameraStatus(CameraStatusChanged message)
        {
            cameraMessage = message.Message;
        }

        private void HandleVisionStatus(VisionStatusChanged message)
        {
            visionMessage = message.Message;
        }

        private void HandleHands(HandTrackingResult message)
        {
            detectedHands = message.Hands.Count;
            palmNormals[Handedness.Left] = Vector3.zero;
            palmNormals[Handedness.Right] = Vector3.zero;
            palmDirections[Handedness.Left] = PalmDirection.Unknown;
            palmDirections[Handedness.Right] = PalmDirection.Unknown;

            for (var index = 0; index < message.Hands.Count; index++)
            {
                var hand = message.Hands[index];
                if (hand == null || !palmNormals.ContainsKey(hand.Handedness))
                {
                    continue;
                }

                palmNormals[hand.Handedness] = hand.PalmNormal;
                palmDirections[hand.Handedness] = palmClassifier.Classify(hand);
                if (palmDirections[hand.Handedness] == PalmDirection.Forward)
                {
                    lastForwardTimestamps[hand.Handedness] = message.Timestamp;
                }
            }

            if (palmDirections[Handedness.Left] == PalmDirection.Forward ||
                palmDirections[Handedness.Right] == PalmDirection.Forward)
            {
                palmForwardFrames++;
            }
        }

        private void HandleGesture(GestureDetected message)
        {
            gestures[message.Hand] = message.Gesture;
            gestureTimestamps[message.Hand] = message.Timestamp;
            if (message.Gesture == GestureType.OpenPalm)
            {
                openPalmEvents++;
                lastOpenPalmTimestamps[message.Hand] = message.Timestamp;
            }
        }

        private void HandleGestureLost(GestureLost message)
        {
            gestures[message.Hand] = GestureType.Unknown;
            motions[message.Hand] = MotionType.Unknown;
        }

        private void HandleMotion(MotionDetected message)
        {
            motions[message.Hand] = message.Motion;
            if (message.Motion == MotionType.Push)
            {
                pushEvents++;
                var reason = DescribeRejection(message);
                lastRejection = reason;
                Debug.Log($"[MagicMotion.Diag] push hand={message.Hand} " +
                          $"conf={message.Confidence:0.00} -> {(reason == "none" ? "CAST" : reason)}");
            }
        }

        private string DescribeRejection(MotionDetected message)
        {
            var gesture = gestures[message.Hand];
            var gestureOk = gesture == GestureType.OpenPalm ||
                message.Timestamp - lastOpenPalmTimestamps[message.Hand] <= DiagnosticGestureMemory;
            var palmFresh = message.Timestamp - lastForwardTimestamps[message.Hand] <= DiagnosticPalmMaxAge;
            // Always report the palm too: a stale palm was previously hidden
            // behind the gesture verdict, which made a palm problem look like a
            // gesture classification problem.
            if (!gestureOk)
            {
                return $"gesture={gesture},palm={palmDirections[message.Hand]},fresh={palmFresh}";
            }

            if (!palmFresh)
            {
                return $"palm={palmDirections[message.Hand]}";
            }

            return "none";
        }

        private void HandleSkillState(SkillStateChanged message)
        {
            skills[message.Hand] = $"{message.SkillId}:{message.State}";
            if (message.State == SkillState.Cast)
            {
                castEvents++;
                Debug.Log($"[MagicMotion.Diag] CAST hand={message.Hand} skill={message.SkillId}");
            }
        }

        private void HandleDamage(DamageApplied message)
        {
            enemyHealth = message.RemainingHealth;
            battleStatus = $"Hit by {message.SourceId} (-{message.AppliedDamage:0})";
        }

        private void HandleBattleCompleted(BattleCompleted message)
        {
            battleStatus = $"Completed ({message.DefeatedEnemies} defeated)";
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTexture(new Color(0.015f, 0.025f, 0.045f, 0.88f));
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15f * Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1f, 1f, 2f)),
                normal = { textColor = new Color(0.45f, 1f, 0.88f) },
                wordWrap = true
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            var events = GameManager.Instance.Events;
            events.Unsubscribe<CameraStatusChanged>(HandleCameraStatus);
            events.Unsubscribe<VisionStatusChanged>(HandleVisionStatus);
            events.Unsubscribe<HandTrackingResult>(HandleHands);
            events.Unsubscribe<GestureDetected>(HandleGesture);
            events.Unsubscribe<GestureLost>(HandleGestureLost);
            events.Unsubscribe<MotionDetected>(HandleMotion);
            events.Unsubscribe<SkillStateChanged>(HandleSkillState);
            events.Unsubscribe<DamageApplied>(HandleDamage);
            events.Unsubscribe<BattleCompleted>(HandleBattleCompleted);
        }
    }
}

