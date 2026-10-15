using System;
using MagicMotion.Camera;

namespace MagicMotion.Vision
{
    /// <summary>
    /// Converts a camera frame into canonical normalized landmarks. X increases right,
    /// Y increases up, and Z decreases as a point moves toward the camera.
    /// </summary>
    public interface IHandTrackingProvider
    {
        event Action<HandTrackingResult> ResultReady;
        bool IsInitialized { get; }
        void Initialize();
        void ProcessFrame(CameraFrame frame);
        void Shutdown();
    }
}

