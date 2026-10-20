using System;
using System.Collections;
using MagicMotion.Config;
using MagicMotion.Core;
using UnityEngine;

namespace MagicMotion.Camera
{
    [DisallowMultipleComponent]
    public sealed class CameraManager : MonoBehaviour, ICameraFrameSource
    {
        private WebCamTexture cameraTexture;
        private WebCamDevice activeDevice;
        private long sequence;
        private Coroutine startupRoutine;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool permissionPromptShown;
        private bool permissionPromptDismissed;
#endif

        public event Action<CameraFrame> FrameReady;

        public CameraState State { get; private set; } = CameraState.Stopped;
        public bool IsRunning => State == CameraState.Running && cameraTexture != null && cameraTexture.isPlaying;
        public WebCamTexture Texture => cameraTexture;
        public bool IsFrontFacing => cameraTexture != null && activeDevice.isFrontFacing;

        public void StartCamera()
        {
            if (IsRunning || startupRoutine != null)
            {
                return;
            }

            startupRoutine = StartCoroutine(StartCameraRoutine());
        }

        public void StopCamera()
        {
            if (startupRoutine != null)
            {
                StopCoroutine(startupRoutine);
                startupRoutine = null;
            }

            if (cameraTexture != null)
            {
                if (cameraTexture.isPlaying)
                {
                    cameraTexture.Stop();
                }

                Destroy(cameraTexture);
                cameraTexture = null;
            }

            SetState(CameraState.Stopped, "Camera stopped.");
        }

        private IEnumerator StartCameraRoutine()
        {
            // The Android runtime permission dialog is only dispatched once the Activity is resumed.
            yield return null;
            yield return null;

            yield return RequestCameraPermission();

            if (!IsCameraPermissionGranted())
            {
                startupRoutine = null;
                SetState(CameraState.PermissionDenied, "Camera permission was denied.");
                yield break;
            }

            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                startupRoutine = null;
                SetState(CameraState.NoDevice, "No camera device is available.");
                yield break;
            }

            var config = FindFirstObjectByType<ConfigManager>()?.Game ?? new GameConfig();
            activeDevice = SelectDevice(devices, config.preferFrontCamera);
            cameraTexture = new WebCamTexture(
                activeDevice.name,
                config.cameraWidth,
                config.cameraHeight,
                config.cameraFrameRate);

            SetState(CameraState.Starting, $"Starting camera: {activeDevice.name}");
            cameraTexture.Play();

            const float timeoutSeconds = 10f;
            var elapsed = 0f;
            while (cameraTexture.width <= 16 && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            startupRoutine = null;
            if (cameraTexture.width <= 16)
            {
                SetState(CameraState.Failed, "Camera did not produce frames before timeout.");
                StopTextureWithoutChangingState();
                yield break;
            }

            sequence = 0;
            SetState(
                CameraState.Running,
                $"Camera is running ({cameraTexture.width}x{cameraTexture.height}@{config.cameraFrameRate}).");
        }

        private static bool IsCameraPermissionGranted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return HasAndroidCameraPermission();
#else
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
        }

        private IEnumerator RequestCameraPermission()
        {
            SetState(CameraState.RequestingPermission, "Requesting camera permission.");
            if (IsCameraPermissionGranted())
            {
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // The system dialog has no completion callback: losing focus means it appeared,
            // regaining focus means the user answered it.
            permissionPromptShown = false;
            permissionPromptDismissed = false;
            RequestAndroidCameraPermission();
            yield return WaitForCondition(() => permissionPromptShown || IsCameraPermissionGranted(), 2.5f);
            if (IsCameraPermissionGranted())
            {
                yield break;
            }

            if (!permissionPromptShown)
            {
                // No dialog at all: the request was dropped or the permission is permanently denied.
                Debug.Log("[MagicMotion.Camera] No camera permission dialog was shown, requesting again.");
                permissionPromptShown = false;
                permissionPromptDismissed = false;
                RequestAndroidCameraPermission();
                yield return WaitForCondition(() => permissionPromptShown || IsCameraPermissionGranted(), 2.5f);
                yield break;
            }

            yield return WaitForCondition(() => permissionPromptDismissed || IsCameraPermissionGranted(), 120f);
            if (!IsCameraPermissionGranted())
            {
                // Give the OS a short grace period to apply the result before reporting the denial.
                yield return new WaitForSecondsRealtime(0.25f);
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds)
        {
            const float pollInterval = 0.2f;
            var poll = new WaitForSecondsRealtime(pollInterval);
            var elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (condition())
                {
                    yield break;
                }

                yield return poll;
                elapsed += pollInterval;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                permissionPromptDismissed = true;
            }
            else
            {
                permissionPromptShown = true;
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string AndroidCameraPermission = "android.permission.CAMERA";

        private static bool HasAndroidCameraPermission()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    // PackageManager.PERMISSION_GRANTED == 0
                    return activity.Call<int>("checkSelfPermission", AndroidCameraPermission) == 0;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[MagicMotion.Camera] Camera permission check failed: {exception.Message}");
                return Application.HasUserAuthorization(UserAuthorization.WebCam);
            }
        }

        private static void RequestAndroidCameraPermission()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("requestPermissions", new object[] { new[] { AndroidCameraPermission }, 1001 });
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[MagicMotion.Camera] Camera permission request failed: {exception.Message}");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            }
        }
#endif

        private void Update()
        {
            if (!IsRunning || !cameraTexture.didUpdateThisFrame)
            {
                return;
            }

            sequence++;
            FrameReady?.Invoke(new CameraFrame(
                cameraTexture,
                sequence,
                Time.realtimeSinceStartupAsDouble,
                cameraTexture.videoRotationAngle,
                cameraTexture.videoVerticallyMirrored,
                activeDevice.isFrontFacing));
        }

        private void OnDisable()
        {
            StopCamera();
        }

        private static WebCamDevice SelectDevice(WebCamDevice[] devices, bool preferFrontCamera)
        {
            for (var index = 0; index < devices.Length; index++)
            {
                if (devices[index].isFrontFacing == preferFrontCamera)
                {
                    return devices[index];
                }
            }

            return devices[0];
        }

        private void StopTextureWithoutChangingState()
        {
            if (cameraTexture == null)
            {
                return;
            }

            if (cameraTexture.isPlaying)
            {
                cameraTexture.Stop();
            }

            Destroy(cameraTexture);
            cameraTexture = null;
        }

        private void SetState(CameraState next, string message)
        {
            State = next;
            GameManager.Instance?.Events.Publish(new CameraStatusChanged(next, message));
            Debug.Log($"[MagicMotion.Camera] {next}: {message}");
        }
    }
}

