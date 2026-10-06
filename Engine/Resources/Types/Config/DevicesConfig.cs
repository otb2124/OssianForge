using OssianForge.Engine.Devices;

namespace OssianForge.Engine.Resources.Config
{
    /// <summary>
    /// The user's choice of devices, stored as a ConfigFile like TreeConfig stores the main scene.
    /// Each device kind has its own block in the JSON:
    ///   "mode"    primary | all | firstN
    ///   "max"     only for firstN
    ///   "current" Id (or part of the name) of the chosen device; empty = automatic (OS default)
    /// The flat keys look like "devices.camera.current", the same way ConfigFile flattens every JSON.
    /// </summary>
    public class DevicesConfig : ConfigFile
    {
        public double PollIntervalSeconds => GetFloat("pollIntervalSeconds", 1f);
        public bool ProbeCamerasOnLoad => GetBool("probeCamerasOnLoad", true);
        public int CameraProbeCount => GetInt("cameraProbeCount", 4);
        public bool LogChanges => GetBool("logChanges", true);
        public bool ProbeCameraModes => GetBool("probeCameraModes", true);

        /// <summary>Capture resolution for the primary camera; 0 = automatic.</summary>
        public int CameraWidth => GetInt("devices.camera.width", 0);
        public int CameraHeight => GetInt("devices.camera.height", 0);

        public DevicesConfig(string id, string path) : base(id, path) { }

        private static string Key(DeviceKind kind) => $"devices.{kind.ToString().ToLowerInvariant()}";

        /// <summary>The stored choice for a kind, or "" when it is automatic.</summary>
        public string GetCurrent(DeviceKind kind) => GetString($"{Key(kind)}.current");

        /// <summary>Reads one kind's block. Missing or invalid values fall back to <paramref name="fallback"/>.</summary>
        public DevicePolicy GetPolicy(DeviceKind kind, DevicePolicy fallback)
        {
            string key = Key(kind);

            var mode = Enum.TryParse<DeviceMode>(GetString($"{key}.mode"), true, out var parsed)
                ? parsed
                : fallback.Mode;

            int max = GetInt($"{key}.max", fallback.Max);
            string current = GetString($"{key}.current");

            return new DevicePolicy(mode, max, string.IsNullOrWhiteSpace(current) ? fallback.PreferredName : current);
        }

        /// <summary>Builds the options Devices runs with: built-in defaults overridden by whatever the file sets.</summary>
        public DevicesOptions ToOptions()
        {
            var options = new DevicesOptions
            {
                PollIntervalSeconds = PollIntervalSeconds,
                ProbeCamerasOnLoad = ProbeCamerasOnLoad,
                CameraProbeCount = CameraProbeCount,
                ProbeCameraModes = ProbeCameraModes,
                CameraWidth = CameraWidth,
                CameraHeight = CameraHeight,
                LogChanges = LogChanges
            };

            foreach (var kind in Enum.GetValues<DeviceKind>())
                options.Policies[kind] = GetPolicy(kind, options.PolicyFor(kind));

            return options;
        }

        public void SetCurrent(DeviceKind kind, string? idOrName, bool save = true)
        {
            Set($"{Key(kind)}.current", idOrName ?? "");
            if (save) Save();
        }

        public void SetPolicy(DeviceKind kind, DevicePolicy policy, bool save = true)
        {
            string key = Key(kind);
            Set($"{key}.mode", policy.Mode.ToString().ToLowerInvariant());
            Set($"{key}.max", policy.Max);
            Set($"{key}.current", policy.PreferredName ?? "");
            if (save) Save();
        }
    }
}