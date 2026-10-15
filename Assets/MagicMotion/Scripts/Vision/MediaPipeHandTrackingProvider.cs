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

        // At one metre and beyond the hand occupies a small part of the frame.
        // Use a lower detector threshold for recall, while gesture, palm,
        // motion and temporal gates continue to reject false skill casts.
        [SerializeField, Range(0f, 1f)] private float minimumDetectionConfidence = 0.40f;
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

            var model = Resources.Load<TextAsset>(ModelResourcePath);
            if (model == null || model.bytes == null || model.bytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"MediaPipe model is missing at Resources/{ModelResourcePath}.bytes");
            }

            var baseOptions = new BaseOptions(
                BaseOptions.Delegate.CPU,
                modelAssetBuffer: model.bytes);
            var options = new HandLandmarkerOptions(
                baseOptions,
                RunningMode.VIDEO,
                numHands: 2,
                minHandDetectionConfidence: minimumDetectionConfidence,
                minHandPresenceConfidence: minimumPresenceConfidence,
                minTrackingConfidence: minimumTrackingConfidence);

            handLandmarker = HandLandmarker.CreateFromOptions(options);
            reusableResult = HandLandmarkerResult.Alloc(2);
            lastTimestampMilliseconds = -1;
            Debug.Log("[MagicMotion.Vision] MediaPipe Hand Landmarker initialized (CPU, two hands).");
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
            frame.Texture.GetPixels32(pixelBuffer);
            CorrectCameraMirroring(pixelBuffer, frame.Texture.width, frame.Texture.height,
                frame.FrontFacing, frame.VerticallyMirrored);
            lastFrameLuma = CalculateLuma(pixelBuffer);
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
                    $"palmWidth={lastMaxPalmWidth:0.000} rotation={frame.RotationDegrees} " +
                    $"mirrorV={frame.VerticallyMirrored} front={frame.FrontFacing}");
            }

            ResultReady?.Invoke(new HandTrackingResult(
                frame.Sequence,
                frame.Timestamp,
                convertedHands.ToArray()));
        }

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
