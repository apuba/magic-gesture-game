using System;

namespace MagicMotion.Camera
{
    public interface ICameraFrameSource
    {
        event Action<CameraFrame> FrameReady;
        CameraState State { get; }
        bool IsRunning { get; }
        void StartCamera();
        void StopCamera();
    }
}

