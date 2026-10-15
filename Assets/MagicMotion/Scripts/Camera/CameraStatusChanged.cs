namespace MagicMotion.Camera
{
    public readonly struct CameraStatusChanged
    {
        public CameraStatusChanged(CameraState state, string message)
        {
            State = state;
            Message = message;
        }

        public CameraState State { get; }
        public string Message { get; }
    }
}

