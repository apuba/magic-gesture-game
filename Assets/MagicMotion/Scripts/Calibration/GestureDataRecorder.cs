using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MagicMotion.Core;
using MagicMotion.Gesture;
using MagicMotion.Motion;
using MagicMotion.Skill;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Calibration
{
    [DisallowMultipleComponent]
    public sealed class GestureDataRecorder : MonoBehaviour
    {
        private enum RecorderPhase
        {
            Idle,
            Ready,
            Countdown,
            Recording,
            Review,
            Complete,
            Error
        }

        [Serializable]
        private sealed class SessionManifest
        {
            public string schemaVersion = "1.0";
            public string sessionId;
            public string gesture = "push";
            public string startedUtc;
            public string deviceModel;
            public string operatingSystem;
            public string unityVersion;
            public int positiveTrials;
            public int negativeTrials;
            public int totalTrials;
            public float countdownSeconds;
            public float recordingSeconds;
            public bool containsVideo;
            public GestureRecordingTrial[] plan;
        }

        [Serializable]
        private sealed class SessionSummary
        {
            public string sessionId;
            public string completedUtc;
            public int acceptedPositive;
            public int acceptedNegative;
            public int acceptedTotal;
            public int totalAttempts;
            public int totalFrames;
            public int totalEvents;
            public string dataDirectory;
        }

        [Serializable]
        private sealed class TrialRecord
        {
            public string recordType = "trial";
            public string sessionId;
            public int trialId;
            public int attempt;
            public string status;
            public string category;
            public string action;
            public string zone;
            public float distanceMeters;
            public int repetition;
            public string utc;
            public int frames;
            public int events;
        }

        [Serializable]
        private sealed class FrameRecord
        {
            public string recordType = "frame";
            public string sessionId;
            public int trialId;
            public int attempt;
            public string category;
            public string action;
            public string zone;
            public float distanceMeters;
            public long sequence;
            public double timestamp;
            public float trialTime;
            public HandRecord[] hands;
        }

        [Serializable]
        private sealed class HandRecord
        {
            public string handedness;
            public float handednessConfidence;
            public string rawGesture;
            public string palmDirection;
            public VectorRecord palmCenter;
            public VectorRecord palmNormal;
            public float palmWidth;
            public float meanDepth;
            public LandmarkRecord[] landmarks;
        }

        [Serializable]
        private sealed class LandmarkRecord
        {
            public int index;
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class VectorRecord
        {
            public float x;
            public float y;
            public float z;

            public static VectorRecord From(Vector3 value)
            {
                return new VectorRecord { x = value.x, y = value.y, z = value.z };
            }
        }

        [Serializable]
        private sealed class AlgorithmEventRecord
        {
            public string recordType = "event";
            public string sessionId;
            public int trialId;
            public int attempt;
            public double timestamp;
            public float trialTime;
            public string eventType;
            public string hand;
            public string gesture;
            public string motion;
            public string skillId;
            public float confidence;
            public float velocity;
        }

        private const float CountdownSeconds = 3f;
        private const float RecordingSeconds = 2f;

        // Button diameters as a share of screen width; big enough to hit from
        // arm's length while standing at the capture distance.
        private const float LargeButton = 0.40f;
        private const float MediumButton = 0.30f;
        private const float SmallButton = 0.17f;

        private readonly List<GestureRecordingTrial> plan = new List<GestureRecordingTrial>(45);
        private readonly BasicGestureClassifier rawGestureClassifier = new BasicGestureClassifier();
        private readonly PalmDirectionClassifier palmDirectionClassifier = new PalmDirectionClassifier();

        private RecorderPhase phase;
        private StreamWriter frameWriter;
        private StreamWriter eventWriter;
        private StreamWriter trialWriter;
        private string sessionId;
        private string sessionDirectory;
        private string errorMessage;
        private int trialIndex;
        private int attempt;
        private int acceptedPositive;
        private int acceptedNegative;
        private int totalAttempts;
        private int totalFrames;
        private int totalEvents;
        private int currentTrialFrames;
        private int currentTrialEvents;
        private float phaseEndsAt;
        private float recordingStartedAt;
        private GUIStyle bodyStyle;
        private GUIStyle bannerStyle;
        private GUIStyle headlineStyle;
        private GUIStyle giantStyle;
        private GUIStyle circleTextLargeStyle;
        private GUIStyle circleTextMediumStyle;
        private GUIStyle circleTextSmallStyle;
        private Texture2D circlePrimaryTexture;
        private Texture2D circleSecondaryTexture;
        private Texture2D circleStopTexture;
        private Texture2D bannerBackground;
        private AndroidJavaObject textToSpeech;
        private bool speechAvailable;
        private int lastCountdownSecond;

        private GestureRecordingTrial CurrentTrial =>
            trialIndex >= 0 && trialIndex < plan.Count ? plan[trialIndex] : null;

        // While a session runs this recorder owns the screen and the voice: it
        // speaks its own countdown and cues, so other prompts must stay quiet
        // or the two talk over each other.
        public bool IsSessionActive =>
            phase != RecorderPhase.Idle &&
            phase != RecorderPhase.Complete &&
            phase != RecorderPhase.Error;

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                enabled = false;
                return;
            }

            GameManager.Instance.Events.Subscribe<HandTrackingResult>(HandleHands);
            GameManager.Instance.Events.Subscribe<GestureDetected>(HandleGesture);
            GameManager.Instance.Events.Subscribe<MotionDetected>(HandleMotion);
            GameManager.Instance.Events.Subscribe<SkillCast>(HandleSkillCast);
            InitializeSpeech();
        }

        private void Update()
        {
            if (phase == RecorderPhase.Countdown && Time.realtimeSinceStartup >= phaseEndsAt)
            {
                BeginRecording();
            }
            else if (phase == RecorderPhase.Recording && Time.realtimeSinceStartup >= phaseEndsAt)
            {
                FinishRecording();
            }

            AnnounceCountdown();
        }

        private void OnGUI()
        {
            EnsureStyles();

            // The phone is read from up to 1.5 m away while the hand is busy, so
            // the layout is four stacked bands that keep clear of the debug HUD
            // at the top: instruction, giant cue, big circular buttons, status.
            DrawInstructionBanner();
            DrawGiantCue();
            DrawActionButtons();
            DrawStatusPanel();
        }

        // The recorder is driven from half a metre away or more, where the small
        // text in the bottom panel is unreadable. Every active phase repeats the
        // instruction as a full-width banner plus a giant countdown / action cue,
        // and the same text is spoken so the trial can be performed without
        // staring at the phone. The band starts below the debug HUD so the two
        // never overlap.
        private void DrawInstructionBanner()
        {
            if (phase == RecorderPhase.Idle ||
                phase == RecorderPhase.Complete ||
                phase == RecorderPhase.Error ||
                CurrentTrial == null)
            {
                return;
            }

            // Placed under the buttons: the top of the screen belongs to the
            // debug HUD, and anything drawn there ends up overprinted by it.
            var rect = new Rect(
                Screen.width * 0.03f,
                Screen.height * 0.75f,
                Screen.width * 0.94f,
                Screen.height * 0.14f);
            GUI.Box(rect, GUIContent.none, bannerStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Label(BannerHeader(), headlineStyle);
            GUILayout.Label(CurrentTrial.Instruction, bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawGiantCue()
        {
            // In Ready the cue is the where, during the countdown it is the when,
            // and during recording it is the what: one word per phase so the
            // three-step loop reads correctly without instructions.
            if (phase == RecorderPhase.Ready)
            {
                DrawGiantText(ShortCue(), new Color(0.35f, 0.95f, 1f));
            }
            else if (phase == RecorderPhase.Countdown)
            {
                DrawGiantText(
                    Mathf.Max(1, Mathf.CeilToInt(phaseEndsAt - Time.realtimeSinceStartup)).ToString(),
                    new Color(1f, 0.85f, 0.2f));
            }
            else if (phase == RecorderPhase.Recording)
            {
                var remaining = Mathf.Max(0f, phaseEndsAt - Time.realtimeSinceStartup);
                DrawGiantText($"{ShortCue()} {remaining:0.0}", new Color(1f, 0.35f, 0.2f));
            }
        }

        private string ShortCue()
        {
            if (CurrentTrial == null)
            {
                return string.Empty;
            }

            return CurrentTrial.IsPositive
                ? GestureRecordingPlan.ZoneShortName(CurrentTrial.zone)
                : GestureRecordingPlan.ActionShortName(CurrentTrial.action);
        }

        private void DrawGiantText(string value, Color color)
        {
            var rect = new Rect(
                Screen.width * 0.05f,
                Screen.height * 0.26f,
                Screen.width * 0.90f,
                Screen.height * 0.20f);
            var previous = GUI.color;
            GUI.color = color;
            GUILayout.BeginArea(rect);
            GUILayout.Label(value, giantStyle);
            GUILayout.EndArea();
            GUI.color = previous;
        }

        // Every control is a large circle with a Chinese label: the phone is
        // touched from arm's length between trials, so small rectangular buttons
        // are easy to miss.
        private void DrawActionButtons()
        {
            switch (phase)
            {
                case RecorderPhase.Idle:
                    if (CircleButton(CircleRect(0.5f, 0.56f, LargeButton), "开始采集", circlePrimaryTexture, circleTextLargeStyle))
                    {
                        StartSession();
                    }
                    break;
                case RecorderPhase.Ready:
                    if (CircleButton(CircleRect(0.5f, 0.56f, LargeButton), "开始", circlePrimaryTexture, circleTextLargeStyle))
                    {
                        BeginCountdown();
                    }

                    DrawStopButton();
                    break;
                case RecorderPhase.Review:
                    if (CircleButton(CircleRect(0.28f, 0.56f, MediumButton), "接受", circlePrimaryTexture, circleTextMediumStyle))
                    {
                        AcceptTrial();
                    }

                    if (CircleButton(CircleRect(0.70f, 0.56f, MediumButton), "重录", circleSecondaryTexture, circleTextMediumStyle))
                    {
                        RejectAndRetry();
                    }

                    DrawStopButton();
                    break;
                case RecorderPhase.Complete:
                case RecorderPhase.Error:
                    if (CircleButton(CircleRect(0.5f, 0.56f, MediumButton), "关闭", circleSecondaryTexture, circleTextMediumStyle))
                    {
                        phase = RecorderPhase.Idle;
                    }
                    break;
            }
        }

        // Stop sits in the top-right corner: the top-left band is owned by the
        // debug HUD, and the area below the action buttons holds the instruction.
        private void DrawStopButton()
        {
            if (CircleButton(CircleRect(0.85f, 0.50f, SmallButton), "结束", circleStopTexture, circleTextSmallStyle))
            {
                StopSession("stopped");
            }
        }

        private Rect CircleRect(float centerX, float centerY, float diameterFraction)
        {
            var diameter = Screen.width * diameterFraction;
            return new Rect(
                Screen.width * centerX - diameter * 0.5f,
                Screen.height * centerY - diameter * 0.5f,
                diameter,
                diameter);
        }

        private static bool CircleButton(Rect rect, string label, Texture2D texture, GUIStyle textStyle)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            return GUI.Button(rect, label, textStyle);
        }

        private void DrawStatusPanel()
        {
            var rect = new Rect(
                Screen.width * 0.03f,
                Screen.height * 0.91f,
                Screen.width * 0.94f,
                Screen.height * 0.08f);
            GUILayout.BeginArea(rect);
            GUILayout.Label(StatusText(), bodyStyle);
            GUILayout.EndArea();
        }

        private string StatusText()
        {
            switch (phase)
            {
                case RecorderPhase.Idle:
                    return "推掌数据采集：30 个正样本 + 15 个负样本，只保存数值关键点，不保存视频";
                case RecorderPhase.Ready:
                    return $"{ProgressHeader()} · 本次只做 1 次：点「开始」→ 倒计时内就位 → 听到「开始」再做";
                case RecorderPhase.Countdown:
                    return $"{ProgressHeader()} · 就位，先别做动作";
                case RecorderPhase.Recording:
                    return $"{ProgressHeader()} · 现在做 1 次 {ShortCue()}，已采集 {currentTrialFrames} 帧";
                case RecorderPhase.Review:
                    return $"{ProgressHeader()} · 本次 {currentTrialFrames} 帧 / {currentTrialEvents} 条算法事件 · 对就「接受」，不对就「重录」";
                case RecorderPhase.Complete:
                    return $"采集完成：正样本 {acceptedPositive}/30，负样本 {acceptedNegative}/15 · {sessionDirectory}";
                case RecorderPhase.Error:
                    return $"录制出错：{errorMessage}";
                default:
                    return string.Empty;
            }
        }

        private string BannerHeader()
        {
            var kindProgress = CurrentTrial.IsPositive
                ? $"正样本 {acceptedPositive + 1}/30"
                : $"负样本 {acceptedNegative + 1}/15";
            var stage = phase switch
            {
                RecorderPhase.Countdown => "准备",
                RecorderPhase.Recording => "录制中",
                RecorderPhase.Review => "确认",
                _ => "按「开始」"
            };
            return $"第 {trialIndex + 1}/45 次 · {kindProgress} · {stage}";
        }

        private string ProgressHeader()
        {
            var kindProgress = CurrentTrial != null && CurrentTrial.IsPositive
                ? $"正样本 {acceptedPositive + 1}/30"
                : $"负样本 {acceptedNegative + 1}/15";
            return $"第 {trialIndex + 1}/45 次 · {kindProgress}";
        }

        private void StartSession()
        {
            try
            {
                CloseWriters();
                plan.Clear();
                plan.AddRange(GestureRecordingPlan.CreatePushCalibrationPlan());
                sessionId = $"push-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                sessionDirectory = Path.Combine(Application.persistentDataPath, "GestureRecordings", sessionId);
                Directory.CreateDirectory(sessionDirectory);

                frameWriter = CreateWriter("frames.jsonl");
                eventWriter = CreateWriter("events.jsonl");
                trialWriter = CreateWriter("trials.jsonl");

                var manifest = new SessionManifest
                {
                    sessionId = sessionId,
                    startedUtc = DateTime.UtcNow.ToString("O"),
                    deviceModel = SystemInfo.deviceModel,
                    operatingSystem = SystemInfo.operatingSystem,
                    unityVersion = Application.unityVersion,
                    positiveTrials = 30,
                    negativeTrials = 15,
                    totalTrials = plan.Count,
                    countdownSeconds = CountdownSeconds,
                    recordingSeconds = RecordingSeconds,
                    containsVideo = false,
                    plan = plan.ToArray()
                };
                File.WriteAllText(
                    Path.Combine(sessionDirectory, "manifest.json"),
                    JsonUtility.ToJson(manifest, true),
                    Encoding.UTF8);

                trialIndex = 0;
                attempt = 0;
                acceptedPositive = 0;
                acceptedNegative = 0;
                totalAttempts = 0;
                totalFrames = 0;
                totalEvents = 0;
                phase = RecorderPhase.Ready;
                Debug.Log($"[MagicMotion.Recorder] Session started: {sessionDirectory}");
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private StreamWriter CreateWriter(string fileName)
        {
            return new StreamWriter(
                Path.Combine(sessionDirectory, fileName),
                false,
                new UTF8Encoding(false),
                65536);
        }

        private void BeginCountdown()
        {
            if (CurrentTrial == null)
            {
                return;
            }

            phase = RecorderPhase.Countdown;
            phaseEndsAt = Time.realtimeSinceStartup + CountdownSeconds;
            lastCountdownSecond = 0;
            Speak(SpeechForTrial(CurrentTrial));
        }

        private void AnnounceCountdown()
        {
            if (phase != RecorderPhase.Countdown)
            {
                return;
            }

            var remaining = Mathf.Max(1, Mathf.CeilToInt(phaseEndsAt - Time.realtimeSinceStartup));
            if (remaining == lastCountdownSecond)
            {
                return;
            }

            lastCountdownSecond = remaining;
            Speak(remaining switch
            {
                3 => "三",
                2 => "二",
                _ => "一"
            });
        }

        private string SpeechForTrial(GestureRecordingTrial trial)
        {
            if (trial == null)
            {
                return null;
            }

            return trial.IsPositive
                ? $"{trial.distanceMeters:0.0}米，{GestureRecordingPlan.ZoneName(trial.zone)}，准备推掌"
                : GestureRecordingPlan.ActionName(trial.action);
        }

        private void InitializeSpeech()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    textToSpeech = new AndroidJavaObject(
                        "android.speech.tts.TextToSpeech",
                        activity,
                        new SpeechInitListener(() =>
                        {
                            try
                            {
                                using (var locale = new AndroidJavaObject("java.util.Locale", "zh", "CN"))
                                {
                                    textToSpeech.Call<int>("setLanguage", locale);
                                }

                                speechAvailable = true;
                            }
                            catch (Exception exception)
                            {
                                Debug.LogWarning($"[MagicMotion.Recorder] TTS language failed: {exception.Message}");
                            }
                        }));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MagicMotion.Recorder] TTS unavailable: {exception.Message}");
            }
#endif
        }

        private void Speak(string text)
        {
            if (!speechAvailable || textToSpeech == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                // QUEUE_FLUSH (0) drops anything still queued so the spoken prompt
                // can never lag behind the countdown on screen.
                textToSpeech.Call<int>("speak", text, 0, null, sessionId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MagicMotion.Recorder] TTS speak failed: {exception.Message}");
            }
        }

        private sealed class SpeechInitListener : AndroidJavaProxy
        {
            private readonly Action onReady;

            public SpeechInitListener(Action onReady)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                this.onReady = onReady;
            }

            private void onInit(int status)
            {
                onReady?.Invoke();
            }
        }

        private void BeginRecording()
        {
            try
            {
                attempt++;
                totalAttempts++;
                currentTrialFrames = 0;
                currentTrialEvents = 0;
                recordingStartedAt = Time.realtimeSinceStartup;
                phaseEndsAt = recordingStartedAt + RecordingSeconds;
                phase = RecorderPhase.Recording;
                WriteTrialRecord("started");
                Speak("开始");
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void FinishRecording()
        {
            FlushWriters();
            phase = RecorderPhase.Review;
            // "完成" read as "the gesture was accepted"; it only means the
            // recording window closed.
            Speak("停");
        }

        private void AcceptTrial()
        {
            try
            {
                WriteTrialRecord("accepted");
                if (CurrentTrial.IsPositive)
                {
                    acceptedPositive++;
                }
                else
                {
                    acceptedNegative++;
                }

                trialIndex++;
                attempt = 0;
                if (trialIndex >= plan.Count)
                {
                    CompleteSession();
                }
                else
                {
                    phase = RecorderPhase.Ready;
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void RejectAndRetry()
        {
            try
            {
                WriteTrialRecord("rejected");
                phase = RecorderPhase.Ready;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void CompleteSession()
        {
            var summary = new SessionSummary
            {
                sessionId = sessionId,
                completedUtc = DateTime.UtcNow.ToString("O"),
                acceptedPositive = acceptedPositive,
                acceptedNegative = acceptedNegative,
                acceptedTotal = acceptedPositive + acceptedNegative,
                totalAttempts = totalAttempts,
                totalFrames = totalFrames,
                totalEvents = totalEvents,
                dataDirectory = sessionDirectory
            };
            File.WriteAllText(
                Path.Combine(sessionDirectory, "summary.json"),
                JsonUtility.ToJson(summary, true),
                Encoding.UTF8);
            CloseWriters();
            phase = RecorderPhase.Complete;
            Debug.Log($"[MagicMotion.Recorder] Session complete: {sessionDirectory}");
        }

        private void StopSession(string status)
        {
            try
            {
                if (CurrentTrial != null)
                {
                    WriteTrialRecord(status);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            CloseWriters();
            phase = RecorderPhase.Idle;
            Debug.Log($"[MagicMotion.Recorder] Session stopped; partial data kept at {sessionDirectory}");
        }

        private void HandleHands(HandTrackingResult result)
        {
            if (phase != RecorderPhase.Recording || CurrentTrial == null || frameWriter == null)
            {
                return;
            }

            try
            {
                var hands = new HandRecord[result.Hands.Count];
                for (var handIndex = 0; handIndex < result.Hands.Count; handIndex++)
                {
                    hands[handIndex] = CreateHandRecord(result.Hands[handIndex]);
                }

                var record = new FrameRecord
                {
                    sessionId = sessionId,
                    trialId = CurrentTrial.id,
                    attempt = attempt,
                    category = CurrentTrial.category,
                    action = CurrentTrial.action,
                    zone = CurrentTrial.zone,
                    distanceMeters = CurrentTrial.distanceMeters,
                    sequence = result.FrameSequence,
                    timestamp = result.Timestamp,
                    trialTime = Time.realtimeSinceStartup - recordingStartedAt,
                    hands = hands
                };
                frameWriter.WriteLine(JsonUtility.ToJson(record));
                currentTrialFrames++;
                totalFrames++;
                if (currentTrialFrames % 15 == 0)
                {
                    frameWriter.Flush();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private HandRecord CreateHandRecord(HandData hand)
        {
            if (hand == null || !hand.IsValid)
            {
                return new HandRecord { handedness = "Invalid", landmarks = Array.Empty<LandmarkRecord>() };
            }

            var motion = MotionSampleFactory.Create(hand, 0d);
            var gesture = rawGestureClassifier.Classify(hand);
            var landmarks = new LandmarkRecord[hand.Landmarks.Count];
            for (var index = 0; index < hand.Landmarks.Count; index++)
            {
                var landmark = hand.Landmarks[index];
                landmarks[index] = new LandmarkRecord
                {
                    index = landmark.Index,
                    x = landmark.Position.x,
                    y = landmark.Position.y,
                    z = landmark.Position.z
                };
            }

            return new HandRecord
            {
                handedness = hand.Handedness.ToString(),
                handednessConfidence = hand.Confidence,
                rawGesture = gesture.Type.ToString(),
                palmDirection = palmDirectionClassifier.Classify(hand).ToString(),
                palmCenter = VectorRecord.From(motion.PalmCenter),
                palmNormal = VectorRecord.From(hand.PalmNormal),
                palmWidth = motion.PalmWidth,
                meanDepth = motion.MeanDepth,
                landmarks = landmarks
            };
        }

        private void HandleGesture(GestureDetected message)
        {
            WriteAlgorithmEvent("gesture", message.Timestamp, message.Hand.ToString(),
                message.Gesture.ToString(), null, null, message.Confidence, 0f);
        }

        private void HandleMotion(MotionDetected message)
        {
            WriteAlgorithmEvent("motion", message.Timestamp, message.Hand.ToString(),
                null, message.Motion.ToString(), null, message.Confidence, message.Velocity);
        }

        private void HandleSkillCast(SkillCast message)
        {
            WriteAlgorithmEvent("skill_cast", message.Timestamp, message.Hand.ToString(),
                null, null, message.SkillId, 0f, 0f);
        }

        private void WriteAlgorithmEvent(
            string eventType,
            double timestamp,
            string hand,
            string gesture,
            string motion,
            string skillId,
            float confidence,
            float velocity)
        {
            if (phase != RecorderPhase.Recording || CurrentTrial == null || eventWriter == null)
            {
                return;
            }

            try
            {
                var record = new AlgorithmEventRecord
                {
                    sessionId = sessionId,
                    trialId = CurrentTrial.id,
                    attempt = attempt,
                    timestamp = timestamp,
                    trialTime = Time.realtimeSinceStartup - recordingStartedAt,
                    eventType = eventType,
                    hand = hand,
                    gesture = gesture,
                    motion = motion,
                    skillId = skillId,
                    confidence = confidence,
                    velocity = velocity
                };
                eventWriter.WriteLine(JsonUtility.ToJson(record));
                currentTrialEvents++;
                totalEvents++;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void WriteTrialRecord(string status)
        {
            if (trialWriter == null || CurrentTrial == null)
            {
                return;
            }

            var record = new TrialRecord
            {
                sessionId = sessionId,
                trialId = CurrentTrial.id,
                attempt = attempt,
                status = status,
                category = CurrentTrial.category,
                action = CurrentTrial.action,
                zone = CurrentTrial.zone,
                distanceMeters = CurrentTrial.distanceMeters,
                repetition = CurrentTrial.repetition,
                utc = DateTime.UtcNow.ToString("O"),
                frames = currentTrialFrames,
                events = currentTrialEvents
            };
            trialWriter.WriteLine(JsonUtility.ToJson(record));
            trialWriter.Flush();
        }

        private void FlushWriters()
        {
            frameWriter?.Flush();
            eventWriter?.Flush();
            trialWriter?.Flush();
        }

        private void CloseWriters()
        {
            FlushWriters();
            frameWriter?.Dispose();
            eventWriter?.Dispose();
            trialWriter?.Dispose();
            frameWriter = null;
            eventWriter = null;
            trialWriter = null;
        }

        private void Fail(Exception exception)
        {
            errorMessage = exception.Message;
            Debug.LogException(exception);
            CloseWriters();
            phase = RecorderPhase.Error;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushWriters();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Events.Unsubscribe<HandTrackingResult>(HandleHands);
                GameManager.Instance.Events.Unsubscribe<GestureDetected>(HandleGesture);
                GameManager.Instance.Events.Unsubscribe<MotionDetected>(HandleMotion);
                GameManager.Instance.Events.Unsubscribe<SkillCast>(HandleSkillCast);
            }

            CloseWriters();
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null)
            {
                return;
            }

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            bannerBackground = new Texture2D(1, 1);
            bannerBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f));
            bannerBackground.Apply();
            bannerStyle = new GUIStyle(GUI.skin.box);
            bannerStyle.normal.background = bannerBackground;

            // Sized so the prompt stays readable from the 1.5 m capture distance.
            headlineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.45f, 1f, 0.88f) }
            };
            giantStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 150,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.35f, 0.2f) }
            };

            circlePrimaryTexture = CreateCircleTexture(
                new Color(0.13f, 0.70f, 0.34f),
                new Color(0.80f, 1f, 0.88f));
            circleSecondaryTexture = CreateCircleTexture(
                new Color(0.93f, 0.60f, 0.12f),
                new Color(1f, 0.93f, 0.72f));
            circleStopTexture = CreateCircleTexture(
                new Color(0.84f, 0.24f, 0.20f),
                new Color(1f, 0.82f, 0.80f));

            circleTextLargeStyle = CircleTextStyle(52);
            circleTextMediumStyle = CircleTextStyle(44);
            circleTextSmallStyle = CircleTextStyle(30);
        }

        // Button visuals are drawn with GUI.DrawTexture instead of a GUIStyle
        // background: skin-backed circular styles silently failed to render on
        // the device, leaving only the floating label.
        private static GUIStyle CircleTextStyle(int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = null, textColor = Color.white },
                hover = { background = null, textColor = Color.white },
                active = { background = null, textColor = Color.white },
                focused = { background = null, textColor = Color.white }
            };
        }

        private static Texture2D CreateCircleTexture(Color fill, Color border)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f - 2f;
            var inner = size * 0.5f - 12f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offsetX = x - center;
                    var offsetY = y - center;
                    var distance = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    texture.SetPixel(
                        x,
                        y,
                        distance <= inner ? fill : (distance <= outer ? border : Color.clear));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
