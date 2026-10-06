namespace OssianForge.Engine.Devices
{
    public enum DeviceMode
    {
        /// <summary>Use exactly one device: the preferred one, else the OS default, else the first.</summary>
        Primary,

        /// <summary>Use every connected device.</summary>
        All,

        /// <summary>Use at most <see cref="DevicePolicy.Max"/> devices, in preference order.</summary>
        FirstN
    }

    /// <summary>Decides how many devices of one kind are "active" and in which order they are preferred.</summary>
    public sealed class DevicePolicy
    {
        public DeviceMode Mode = DeviceMode.All;

        /// <summary>Only used by <see cref="DeviceMode.FirstN"/>.</summary>
        public int Max = 1;

        /// <summary>Case-insensitive part of a device name (or an exact Id). Matches sort first.</summary>
        public string? PreferredName;

        public DevicePolicy() { }

        public DevicePolicy(DeviceMode mode, int max = 1, string? preferredName = null)
        {
            Mode = mode;
            Max = max;
            PreferredName = preferredName;
        }

        public override string ToString() =>
            Mode == DeviceMode.FirstN ? $"FirstN({Max})" : Mode.ToString();
    }

    public sealed class DevicesOptions
    {
        public double PollIntervalSeconds = 1.0;

        /// <summary>
        /// Cameras cannot be listed without opening them (the privacy light flashes), so they are
        /// probed once at load and then only when RefreshCameras() is called.
        /// </summary>
        public bool ProbeCamerasOnLoad = true;

        /// <summary>Camera indices 0..N-1 are tried when probing.</summary>
        public int CameraProbeCount = 4;

        /// <summary>While probing, also find which capture resolutions each camera delivers.</summary>
        public bool ProbeCameraModes = true;

        /// <summary>Capture resolution to request from the primary camera. 0 = choose automatically from what it delivers.</summary>
        public int CameraWidth = 0;
        public int CameraHeight = 0;

        /// <summary>Print a line for every device that appears or disappears.</summary>
        public bool LogChanges = true;

        public Dictionary<DeviceKind, DevicePolicy> Policies = new()
        {
            // Keyboards and mice are merged by the OS into one logical input, so these only affect reporting for now.
            [DeviceKind.Keyboard] = new DevicePolicy(DeviceMode.All),
            [DeviceKind.Mouse] = new DevicePolicy(DeviceMode.All),
            [DeviceKind.GamePad] = new DevicePolicy(DeviceMode.All),
            [DeviceKind.Monitor] = new DevicePolicy(DeviceMode.Primary),
            [DeviceKind.Camera] = new DevicePolicy(DeviceMode.Primary),
            [DeviceKind.Microphone] = new DevicePolicy(DeviceMode.Primary),
            [DeviceKind.Speaker] = new DevicePolicy(DeviceMode.Primary),
        };

        public DevicePolicy PolicyFor(DeviceKind kind) =>
            Policies.TryGetValue(kind, out var policy) ? policy : new DevicePolicy();
    }
}