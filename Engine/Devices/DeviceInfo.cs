namespace OssianForge.Engine.Devices
{
    public enum DeviceKind
    {
        Keyboard,
        Mouse,
        GamePad,
        Monitor,
        Camera,
        Microphone,
        Speaker
    }

    /// <summary>A capture resolution a camera actually delivers.</summary>
    public readonly record struct CameraMode(int Width, int Height)
    {
        public override string ToString() => $"{Width}x{Height}";
    }

    /// <summary>
    /// One connected device as reported by a provider. It is an immutable snapshot:
    /// devices are matched between polls by <see cref="Id"/>, never by reference.
    /// </summary>
    /// <param name="Id">Stable within a kind (device path, index, ...). Used for hotplug diffing.</param>
    /// <param name="Index">Position inside its kind as the provider sees it; for cameras this is the OpenCV device index.</param>
    /// <param name="IsDefault">The OS default / main device of this kind (main monitor, default mic, first camera, ...).</param>
    /// <param name="Detail">Free text for logs and UIs (resolution, bounds, ...).</param>
    /// <param name="Modes">Cameras only: the capture resolutions the device delivered when probed, smallest first. Null for everything else.</param>
    public sealed record DeviceInfo(
        DeviceKind Kind,
        string Id,
        string Name,
        int Index,
        bool IsDefault,
        string Detail = "",
        IReadOnlyList<CameraMode>? Modes = null)
    {
        public override string ToString()
        {
            string detail = Detail.Length > 0 ? $" {Detail}" : "";
            string def = IsDefault ? " [default]" : "";
            return $"{Kind} #{Index} \"{Name}\"{def}{detail}";
        }
    }
}