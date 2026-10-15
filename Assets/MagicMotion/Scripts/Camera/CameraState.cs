namespace MagicMotion.Camera
{
    public enum CameraState
    {
        Stopped,
        RequestingPermission,
        Starting,
        Running,
        PermissionDenied,
        NoDevice,
        Failed
    }
}

