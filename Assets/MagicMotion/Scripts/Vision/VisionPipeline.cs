using System;
using System.Collections.Generic;
using MagicMotion.Camera;
using MagicMotion.Config;
using MagicMotion.Core;
using UnityEngine;

namespace MagicMotion.Vision
{
    [DisallowMultipleComponent]
    public sealed class VisionPipeline : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour trackingProviderBehaviour;
        [SerializeField, Range(0f, 0.95f)] private float landmarkSmoothing = 0.2f;

        private ICameraFrameSource cameraSource;
        private IHandTrackingProvider trackingProvider;
        private FrameRateGate frameRateGate;
        private int trackingRate = 30;
        private LandmarkSmoother smoother;

        public VisionStatus Status { get; private set; } = VisionStatus.Stopped;
        public string StatusMessage { get; private set; } = "Not started";
        public int FramesReceived { get; private set; }
        public int FramesProcessed { get; private set; }
        public int ResultsPublished { get; private set; }

        public int TrackingRate
        {
            get => trackingRate;
            set
            {
                var clamped = Mathf.Clamp(value, 1, 60);
                if (trackingRate == clamped && frameRateGate != null)
                {
                    return;
                }

                trackingRate = clamped;
                frameRateGate = new FrameRateGate(trackingRate);
            }
        }

        private void Start()
        {
            var cameraManager = FindFirstObjectByType<CameraManager>();
            var configManager = FindFirstObjectByType<ConfigManager>();
            TrackingRate = configManager?.Game?.visionFrameRate ?? 30;
            smoother = new LandmarkSmoother(landmarkSmoothing);
            cameraSource = cameraManager;

            if (trackingProviderBehaviour != null &&
                trackingProviderBehaviour is IHandTrackingProvider provider)
            {
                BindProvider(provider);
            }
            else
            {
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (var index = 0; index < behaviours.Length; index++)
                {
                    if (behaviours[index] is IHandTrackingProvider discoveredProvider)
                    {
                        trackingProviderBehaviour = behaviours[index];
                        BindProvider(discoveredProvider);
                        return;
                    }
                }

                SetStatus(VisionStatus.WaitingForProvider, "No hand-tracking provider is configured.");
            }
        }

        public void BindProvider(IHandTrackingProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (cameraSource == null)
            {
                throw new InvalidOperationException("A camera frame source must be available before binding a provider.");
            }

            UnbindProvider();
            trackingProvider = provider;
            trackingProvider.ResultReady += HandleTrackingResult;
            cameraSource.FrameReady += HandleCameraFrame;

            try
            {
                SetStatus(VisionStatus.Initializing, "Initializing hand-tracking provider.");
                trackingProvider.Initialize();
                SetStatus(VisionStatus.Running, "Vision pipeline is running.");
            }
            catch (Exception exception)
            {
                SetStatus(VisionStatus.Failed, exception.Message);
                Debug.LogException(exception);
            }
        }

        private void HandleCameraFrame(CameraFrame frame)
        {
            if (frame.IsValid)
            {
                FramesReceived++;
            }

            if (Status != VisionStatus.Running ||
                trackingProvider == null ||
                !trackingProvider.IsInitialized ||
                !frame.IsValid ||
                !frameRateGate.ShouldProcess(frame.Timestamp))
            {
                return;
            }

            FramesProcessed++;

            try
            {
                trackingProvider.ProcessFrame(frame);
            }
            catch (Exception exception)
            {
                SetStatus(VisionStatus.Failed, exception.Message);
                Debug.LogException(exception);
            }
        }

        private void HandleTrackingResult(HandTrackingResult result)
        {
            ResultsPublished++;
            var smoothedHands = new List<HandData>(result.Hands.Count);
            var sawLeft = false;
            var sawRight = false;
            for (var index = 0; index < result.Hands.Count; index++)
            {
                var hand = result.Hands[index];
                if (hand == null || !hand.IsValid)
                {
                    continue;
                }

                sawLeft |= hand.Handedness == Handedness.Left;
                sawRight |= hand.Handedness == Handedness.Right;
                smoothedHands.Add(smoother.Process(hand));
            }

            if (!sawLeft)
            {
                smoother.Reset(Handedness.Left);
            }

            if (!sawRight)
            {
                smoother.Reset(Handedness.Right);
            }

            GameManager.Instance?.Events.Publish(new HandTrackingResult(
                result.FrameSequence,
                result.Timestamp,
                smoothedHands,
                result.WidthToHeight));
        }

        private void OnDestroy()
        {
            UnbindProvider();
        }

        private void UnbindProvider()
        {
            if (cameraSource != null)
            {
                cameraSource.FrameReady -= HandleCameraFrame;
            }

            if (trackingProvider != null)
            {
                trackingProvider.ResultReady -= HandleTrackingResult;
                trackingProvider.Shutdown();
                trackingProvider = null;
            }

            frameRateGate?.Reset();
            smoother?.ResetAll();
        }

        private void SetStatus(VisionStatus next, string message)
        {
            Status = next;
            StatusMessage = message;
            GameManager.Instance?.Events.Publish(new VisionStatusChanged(next, message));
            Debug.Log($"[MagicMotion.Vision] {next}: {message}");
        }
    }
}

