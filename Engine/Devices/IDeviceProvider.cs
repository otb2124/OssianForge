namespace OssianForge.Engine.Devices
{
    /// <summary>
    /// Knows how to list one family of devices. Providers only enumerate: they never open a
    /// device for capture, because opening a camera or microphone lights the OS privacy indicator.
    /// </summary>
    public interface IDeviceProvider
    {
        string Name { get; }

        /// <summary>
        /// Appends every device that is connected right now. Called on the poll timer from the
        /// main thread, so it has to be cheap; providers with expensive discovery cache their result.
        /// </summary>
        void Enumerate(List<DeviceInfo> into);
    }
}