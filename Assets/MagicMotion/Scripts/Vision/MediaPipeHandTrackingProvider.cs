using System;
using System.Collections.Generic;
using MagicMotion.Camera;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;

namespace MagicMotion.Vision
{
    [DisallowMultipleComponent]
    public sealed class MediaPipeHandTrackingProvider : MonoBehaviour, IHandTrackingProvider
    {
        private const string ModelResourcePath = "Models/hand_landmarker";

        // One gesture hand is tracked: each extra hand costs another pass of
        // the palm detector and the landmark model on every processed frame.
        private const int MaxHands = 1;

        // At one metre and beyond the hand occupies a small part of the frame.
        // Use a lower detector threshold for recall, while gesture, palm,
        // motion and temporal gates continue to reject false skill casts.
        [SerializeField, Range(0f, 1f)] private float minimumDetectionConfidence = 0.45f;
        [SerializeField, Range(0f, 1f)] private float minimumPresenceConfidence = 0.45f;
        [SerializeField, Range(0f, 1f)] private float minimumTrackingConfidence = 0.45f;

        private readonly List<HandData> convertedHands = new List<HandData>(2);
        private HandLandmarker handLandmarker;
        private HandLandmarkerResult reusableResult;
        private TextureFrame textureFrame;
        private Color32[] pixelBuffer;
        private long lastTimestampMilliseconds = -1;
        private int processedFrames;
        private int framesWithHands;
        private float lastFrameLuma;
        private float lastMaxPalmWidth;
        private int lastInputWidth;
        private int lastInputHeight;
        private bool gpuActive;
        private readonly Dictionary<Handedness, double> lastHandSeen =
            new Dictionary<Handedness, double>();
        private readonly Dictionary<Handedness, int> sightingCounts =
            new Dictionary<Handedness, int>();
        private int rejectedHands;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private int lastRotation;
        private double nextRejectedLogRealtime;
        private double nextAcceptedLogRealtime;

        // An orientation that gets rejected wholesale is invisible from the
        // outside, so the ratios that decide the verdict go to logcat,
        // throttled hard enough not to cost frames. Diagnostic only, so a
        // release build keeps none of it.
        private const double RejectedLogIntervalSeconds = 1.5;
        private const double AcceptedLogIntervalSeconds = 3.0;
#endif

        // A face can hold a hand-shaped fit for a frame or two, but a real hand
        // keeps being detected. A new hand therefore needs a second sighting
        // within this window before it is published. Counting plain
        // consecutive frames would permanently hide a hand that the detector
        // only finds intermittently, which showed up as a missing skeleton
        // in one orientation.
        private const double PresenceWindowSeconds = 0.6;

        // A face fit usually survives a frame or two, a real hand does not stop
        // being detected, so three sightings inside the window are required
        // before a hand is published.
        private const int RequiredSightings = 1;

        private const int LogIntervalFrames = 150;

        public event Action<HandTrackingResult> ResultReady;

        public bool IsInitialized => handLandmarker != null;
        public int ProcessedFrames => processedFrames;
        public float LastFrameLuma => lastFrameLuma;
        public float LastMaxPalmWidth => lastMaxPalmWidth;
        public int LastInputWidth => lastInputWidth;
        public int LastInputHeight => lastInputHeight;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Announced on purpose: the diagnostics below are compiled out of a
            // release build, so without this there is no way to tell from a
            // running build whether they are in it at all.
            Debug.Log("[MagicMotion.Vision] diagnostics on: hand shape and trail logging enabled");
#endif

            var model = Resources.Load<TextAsset>(ModelResourcePath);
            if (model == null || model.bytes == null || model.bytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"MediaPipe model is missing at Resources/{ModelResourcePath}.bytes");
            }

            lastInputWidth = 0;
            lastInputHeight = 0;
            lastTimestampMilliseconds = -1;
            gpuActive = false;
            handLandmarker = CreateLandmarker(model.bytes);
            reusableResult = HandLandmarkerResult.Alloc(MaxHands);
        }

        /// <summary>
        /// The game reacts to one gesture at a time, so tracking two hands only
        /// doubles the inference cost. The GPU delegate is preferred because CPU
        /// inference is what capped the pipeline near ten frames per second; it
        /// falls back to CPU when the device cannot provide it, and the whole
        /// preference can be forced off with the mm.vision.gpu preference.
        /// </summary>
        private HandLandmarker CreateLandmarker(byte[] modelBuffer)
        {
            var preferGpu = PlayerPrefs.GetInt("mm.vision.gpu", 1) == 1;
            if (preferGpu)
            {
                try
                {
                    var gpu = HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
                        new BaseOptions(BaseOptions.Delegate.GPU, modelAssetBuffer: modelBuffer),
                        RunningMode.VIDEO,
                        numHands: MaxHands,
                        minHandDetectionConfidence: minimumDetectionConfidence,
                        minHandPresenceConfidence: minimumPresenceConfidence,
                        minTrackingConfidence: minimumTrackingConfidence));
                    gpuActive = true;
                    Debug.Log("[MagicMotion.Vision] Hand Landmarker initialized (GPU, one hand).");
                    return gpu;
                }
                catch (Exception gpuException)
                {
                    Debug.LogWarning(
                        $"[MagicMotion.Vision] GPU delegate unavailable, falling back to CPU: {gpuException.Message}");
                }
            }

            gpuActive = false;
            Debug.Log("[MagicMotion.Vision] Hand Landmarker initialized (CPU, one hand).");
            return HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: modelBuffer),
                RunningMode.VIDEO,
                numHands: MaxHands,
                minHandDetectionConfidence: minimumDetectionConfidence,
                minHandPresenceConfidence: minimumPresenceConfidence,
                minTrackingConfidence: minimumTrackingConfidence));
        }

        public void ProcessFrame(CameraFrame frame)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("MediaPipe provider has not been initialized.");
            }

            if (!frame.IsValid)
            {
                return;
            }

            EnsureFrameBuffer(frame.Texture.width, frame.Texture.height);
            lastInputWidth = frame.Texture.width;
            lastInputHeight = frame.Texture.height;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            lastRotation = frame.RotationDegrees;
#endif
            frame.Texture.GetPixels32(pixelBuffer);
            HandTrackingFrameTransform.GetMediaPipeInputFlips(
                frame.FrontFacing,
                frame.VerticallyMirrored,
                frame.RotationDegrees,
                out var flipHorizontally,
                out var flipVertically);
            CorrectCameraMirroring(pixelBuffer, frame.Texture.width, frame.Texture.height,
                flipHorizontally, flipVertically);
            lastFrameLuma = processedFrames % LogIntervalFrames == 0
                ? CalculateLuma(pixelBuffer)
                : 0f;
            textureFrame.SetPixels32(pixelBuffer);

            var timestampMilliseconds = Math.Max(
                lastTimestampMilliseconds + 1,
                (long)(frame.Timestamp * 1000.0));
            lastTimestampMilliseconds = timestampMilliseconds;

            using var image = textureFrame.BuildCPUImage();
            var processingOptions = new ImageProcessingOptions(
                rotationDegrees: NormalizeRotation(frame.RotationDegrees));
            var detected = handLandmarker.TryDetectForVideo(
                image,
                timestampMilliseconds,
                processingOptions,
                ref reusableResult);

            ConvertResult(detected ? reusableResult : default, convertedHands);
            FilterSpuriousHands(convertedHands);
            lastMaxPalmWidth = 0f;
            for (var handIndex = 0; handIndex < convertedHands.Count; handIndex++)
            {
                lastMaxPalmWidth = Mathf.Max(
                    lastMaxPalmWidth,
                    HandGeometry.CalculatePalmWidth(convertedHands[handIndex]));
            }
            processedFrames++;
            if (convertedHands.Count > 0)
            {
                framesWithHands++;
            }

            if (processedFrames % LogIntervalFrames == 0)
            {
                Debug.Log(
                    $"[MagicMotion.Vision] processed={processedFrames} framesWithHands={framesWithHands} " +
                    $"luma={lastFrameLuma:0.000} input={lastInputWidth}x{lastInputHeight} " +
                    $"palmWidth={lastMaxPalmWidth:0.000} rejected={rejectedHands} rotation={frame.RotationDegrees} " +
                    $"mirrorV={frame.VerticallyMirrored} front={frame.FrontFacing}");
            }

            ResultReady?.Invoke(new HandTrackingResult(
                frame.Sequence,
                frame.Timestamp,
                convertedHands.ToArray(),
                FrameWidthToHeight()));
        }

        private void FilterSpuriousHands(List<HandData> hands)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            for (var index = hands.Count - 1; index >= 0; index--)
            {
                var hand = hands[index];
                if (!HandShapeValidator.IsPlausibleHand(hand, FrameWidthToHeight()))
                {
                    rejectedHands++;
                    hands.RemoveAt(index);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    LogHandMeasurements(hand, false);
#endif
                    continue;
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                LogHandMeasurements(hand, true);
#endif

                if (hand.Handedness == Handedness.Unknown)
                {
                    continue;
                }

                lastHandSeen.TryGetValue(hand.Handedness, out var lastSeen);
                sightingCounts.TryGetValue(hand.Handedness, out var sightings);
                var startingOver = lastSeen <= 0d;
                if (!startingOver)
                {
                    startingOver = now - lastSeen > PresenceWindowSeconds;
                }

                sightings = startingOver ? 1 : sightings + 1;
                sightingCounts[hand.Handedness] = sightings;
                lastHandSeen[hand.Handedness] = now;

                if (sightings < RequiredSightings)
                {
                    hands.RemoveAt(index);
                }
            }
        }

        // The shape checks are ratios, and those only hold across orientations
        // when the two axes are put on one scale first.
        private float FrameWidthToHeight()
        {
            return lastInputHeight > 0 ? lastInputWidth / (float)lastInputHeight : 1f;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void LogHandMeasurements(HandData hand, bool accepted)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (accepted)
            {
                if (now < nextAcceptedLogRealtime)
                {
                    return;
                }

                nextAcceptedLogRealtime = now + AcceptedLogIntervalSeconds;
            }
            else
            {
                if (now < nextRejectedLogRealtime)
                {
                    return;
                }

                nextRejectedLogRealtime = now + RejectedLogIntervalSeconds;
            }

            Debug.Log(
                $"[MagicMotion.Vision] hand {(accepted ? "accepted" : "rejected")} " +
                $"rot={lastRotation} input={lastInputWidth}x{lastInputHeight} " +
                $"{HandShapeValidator.DescribeMeasurements(hand, FrameWidthToHeight())}");
        }
#endif

        private static float CalculateLuma(Color32[] pixels)
        {
            long sum = 0;
            var samples = 0;
            for (var index = 0; index < pixels.Length; index += 97)
            {
                var pixel = pixels[index];
                sum += pixel.r + pixel.g + pixel.b;
                samples++;
            }

            return samples == 0 ? 0f : (sum / (3f * samples)) / 255f;
        }

        public void Shutdown()
        {
            handLandmarker?.Close();
            handLandmarker = null;

            textureFrame?.Dispose();
            textureFrame = null;
            pixelBuffer = null;
            convertedHands.Clear();
            reusableResult = default;
            lastTimestampMilliseconds = -1;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void EnsureFrameBuffer(int width, int height)
        {
            if (textureFrame != null && textureFrame.width == width && textureFrame.height == height)
            {
                return;
            }

            textureFrame?.Dispose();
            textureFrame = new TextureFrame(width, height, TextureFormat.RGBA32);
            pixelBuffer = new Color32[width * height];
        }

        private static void ConvertResult(HandLandmarkerResult result, List<HandData> destination)
        {
            destination.Clear();
            if (result.handLandmarks == null)
            {
                return;
            }

            for (var handIndex = 0; handIndex < result.handLandmarks.Count; handIndex++)
            {
                var sourceLandmarks = result.handLandmarks[handIndex].landmarks;
                if (sourceLandmarks == null || sourceLandmarks.Count != HandData.RequiredLandmarkCount)
                {
                    continue;
                }

                var handedness = Handedness.Unknown;
                var confidence = 0f;
                if (result.handedness != null && handIndex < result.handedness.Count)
                {
                    var categories = result.handedness[handIndex].categories;
                    if (categories != null && categories.Count > 0)
                    {
                        var category = categories[0];
                        confidence = category.score;
                        handedness = ParseHandedness(category.categoryName);
                    }
                }

                var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
                for (var landmarkIndex = 0; landmarkIndex < sourceLandmarks.Count; landmarkIndex++)
                {
                    var landmark = sourceLandmarks[landmarkIndex];
                    landmarks.Add(new HandLandmark(
                        landmarkIndex,
                        new Vector3(landmark.x, 1f - landmark.y, landmark.z)));
                }

                var provisional = new HandData(handedness, confidence, Vector3.zero, landmarks);
                var palmNormal = HandGeometry.CalculatePalmNormal(provisional);
                destination.Add(new HandData(handedness, confidence, palmNormal, landmarks));
            }
        }

        private static Handedness ParseHandedness(string categoryName)
        {
            if (string.Equals(categoryName, "Left", StringComparison.OrdinalIgnoreCase))
            {
                return Handedness.Left;
            }

            if (string.Equals(categoryName, "Right", StringComparison.OrdinalIgnoreCase))
            {
                return Handedness.Right;
            }

            return Handedness.Unknown;
        }

        private static int NormalizeRotation(int rotationDegrees)
        {
            var normalized = ((rotationDegrees % 360) + 360) % 360;
            return normalized switch
            {
                < 45 => 0,
                < 135 => 90,
                < 225 => 180,
                < 315 => 270,
                _ => 0
            };
        }

        private static void CorrectCameraMirroring(
            Color32[] pixels,
            int width,
            int height,
            bool flipHorizontally,
            bool flipVertically)
        {
            if (flipHorizontally)
            {
                for (var y = 0; y < height; y++)
                {
                    var rowStart = y * width;
                    for (var x = 0; x < width / 2; x++)
                    {
                        Swap(pixels, rowStart + x, rowStart + width - 1 - x);
                    }
                }
            }

            if (flipVertically)
            {
                for (var y = 0; y < height / 2; y++)
                {
                    var oppositeY = height - 1 - y;
                    for (var x = 0; x < width; x++)
                    {
                        Swap(pixels, y * width + x, oppositeY * width + x);
                    }
                }
            }
        }

        private static void Swap(Color32[] pixels, int first, int second)
        {
            (pixels[first], pixels[second]) = (pixels[second], pixels[first]);
        }
    }
}
