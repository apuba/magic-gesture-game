using System;
using MagicMotion.Calibration;
using UnityEngine;

namespace MagicMotion.UI
{
    /// Speaks short guidance out loud using the device's text-to-speech engine.
    /// The on-screen hint is small print on a phone and is easy to miss, so
    /// anything the user has to act on is also said.
    [DisallowMultipleComponent]
    public sealed class SpeechFeedback : MonoBehaviour
    {
        [SerializeField] private float minimumIntervalSeconds = 4f;
        [SerializeField] private bool enabledOnDevice = true;

        private const string UtteranceId = "magicmotion";

        private AndroidJavaObject textToSpeech;
        private GestureDataRecorder recorder;
        private bool ready;
        private float lastSpokeAt = float.NegativeInfinity;
        private string pending;

        private void Start()
        {
            if (!enabledOnDevice || Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            // TODO: GestureDataRecorder builds its own TextToSpeech instance.
            // The two should share one; until then this one defers to the
            // recorder during capture so the prompts do not overlap.
            recorder = FindFirstObjectByType<GestureDataRecorder>();

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    textToSpeech = new AndroidJavaObject(
                        "android.speech.tts.TextToSpeech",
                        activity,
                        new InitListener(this));
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[MagicMotion] Text-to-speech unavailable: {error.Message}");
            }
        }

        public void Speak(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (recorder != null && recorder.IsSessionActive)
            {
                return;
            }

            if (!ready)
            {
                // The engine initialises asynchronously; keep only the newest
                // line rather than queueing a backlog of stale guidance.
                pending = text;
                return;
            }

            if (Time.unscaledTime - lastSpokeAt < minimumIntervalSeconds)
            {
                return;
            }

            lastSpokeAt = Time.unscaledTime;
            try
            {
                // The Bundle overload needs API 21+; the HashMap form still
                // resolves on every supported device.
                var parameters = new AndroidJavaObject("java.util.HashMap");
                textToSpeech.Call<int>("speak", text, 0, parameters);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[MagicMotion] Text-to-speech speak failed: {error.Message}");
            }
        }

        private void HandleReady()
        {
            ready = true;
            try
            {
                var locale = new AndroidJavaObject("java.util.Locale", "zh", "CN");
                var result = textToSpeech.Call<int>("setLanguage", locale);
                Debug.Log($"[MagicMotion] Text-to-speech ready, setLanguage(zh-CN) result={result}");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[MagicMotion] Text-to-speech language failed: {error.Message}");
            }

            if (pending != null)
            {
                var queued = pending;
                pending = null;
                Speak(queued);
            }
        }

        private sealed class InitListener : AndroidJavaProxy
        {
            private readonly SpeechFeedback owner;

            public InitListener(SpeechFeedback owner)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                this.owner = owner;
            }

            // Called on the Android UI thread; named to match the Java interface.
            public void onInit(int status)
            {
                if (status == 0)
                {
                    owner.HandleReady();
                }
                else
                {
                    Debug.LogWarning($"[MagicMotion] Text-to-speech init status={status}");
                }
            }
        }
    }
}
