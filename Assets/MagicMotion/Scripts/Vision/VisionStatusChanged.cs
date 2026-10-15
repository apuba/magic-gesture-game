namespace MagicMotion.Vision
{
    public readonly struct VisionStatusChanged
    {
        public VisionStatusChanged(VisionStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public VisionStatus Status { get; }
        public string Message { get; }
    }
}

