using OssianForge.Engine.Devices.Providers;
using OssianForge.Engine.Inputs;
using OssianForge.Engine.Resources.Config;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace OssianForge.Engine.Devices
{
    /// <summary>
    /// Single source of truth for which devices exist and which of them the engine should use.
    /// It answers "what is connected" (inventory, hotplug) and "what is active" (policy); the
    /// Inputs, Audio and Graphics modules decide what to do with the answer.
    ///
    /// Lifecycle: Initialize() after Create(), OnLoad() first thing in Engine.OnLoad (needs the window,
    /// and must run before anything starts a camera), OnUpdate() every frame. Main thread only.
    /// </summary>
    public sealed class Devices
    {
        public DevicesOptions Options { get; private set; } = new();

        /// <summary>Created here so every module shares one context. Null until OnLoad().</summary>
        public IInputContext? InputContext { get; private set; }

        public event Action<DeviceInfo>? DeviceAdded;
        public event Action<DeviceInfo>? DeviceRemoved;

        /// <summary>The active set of a kind changed (hotplug or policy). Consumers re-read Active(kind).</summary>
        public event Action<DeviceKind>? ActiveDevicesChanged;

        private readonly List<IDeviceProvider> _providers = new();
        private readonly Dictionary<DeviceKind, List<DeviceInfo>> _connected = new();
        private readonly Dictionary<DeviceKind, List<DeviceInfo>> _active = new();
        private readonly HashSet<string> _failedProviders = new();

        private CameraProvider? _cameras;
        private DevicesConfig? _config;
        private double _pollTimer;
        private bool _loaded;

        public Devices()
        {
            foreach (var kind in Enum.GetValues<DeviceKind>())
            {
                _connected[kind] = new List<DeviceInfo>();
                _active[kind] = new List<DeviceInfo>();
            }
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        public void Initialize()
        {
            // Providers that do not need the window.
            if (OperatingSystem.IsWindows())
                _providers.Add(new RawInputProvider());

            _cameras = new CameraProvider { ProbeCount = Options.CameraProbeCount };
            _providers.Add(_cameras);
        }

        public void OnLoad()
        {
            var window = Engine.Graphics.Window;

            InputContext = window.CreateInput();

            _providers.Add(new MonitorProvider());
            _providers.Add(new SilkInputProvider(() => InputContext, includeKeyboardsAndMice: !OperatingSystem.IsWindows()));

            // Discovery waits for ApplyConfig(): the config decides whether and how far to probe cameras.
        }

        /// <summary>
        /// Second step of loading. Call right after Resources.OnLoad() (so 'configfile.devices' is loaded)
        /// and before Graphics.OnLoad() (which starts the head tracker's camera). Works without the config
        /// too and then keeps the built-in defaults.
        /// </summary>
        public void ApplyConfig()
        {
            _config = Engine.Resources.GetResource<DevicesConfig>("configfile.devices");

            if (_config != null)
            {
                Options = _config.ToOptions();
                if (_cameras != null) _cameras.ProbeCount = Options.CameraProbeCount;
            }
            else
            {
                Console.WriteLine("[DEVICES] 'configfile.devices' not found; using built-in defaults.");
            }

            if (Options.ProbeCamerasOnLoad)
                _cameras?.Probe();

            Refresh(raiseEvents: false);
            LogInventory();

            ActiveDevicesChanged += OnActiveDevicesChanged;
            if (HasMonitorChoice) MoveWindowToMonitor();

            // Hand the chosen camera to CameraInput before anything starts it.
            CameraInput.Instance.Initialize(CreateCameraOptions());

            _loaded = true;
        }

        public void OnUpdate(double delta)
        {
            if (!_loaded) return;

            _pollTimer += delta;
            if (_pollTimer < Options.PollIntervalSeconds) return;

            _pollTimer = 0;
            Refresh(raiseEvents: true);
        }

        public void Shutdown()
        {
            InputContext?.Dispose();
            InputContext = null;
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Everything connected, in provider order.</summary>
        public IReadOnlyList<DeviceInfo> Get(DeviceKind kind) => _connected[kind];

        /// <summary>The devices the engine should use, after the policy for this kind.</summary>
        public IReadOnlyList<DeviceInfo> Active(DeviceKind kind) => _active[kind];

        public DeviceInfo? Primary(DeviceKind kind) => _active[kind].FirstOrDefault();

        public int Count(DeviceKind kind) => _connected[kind].Count;

        /// <summary>The monitor the window is currently on, if Silk reports it.</summary>
        public DeviceInfo? WindowMonitor
        {
            get
            {
                int? index = Engine.Graphics?.Window?.Monitor?.Index;
                return index == null ? null : _connected[DeviceKind.Monitor].FirstOrDefault(m => m.Index == index);
            }
        }

        // ── Control ──────────────────────────────────────────────────────────

        public void SetPolicy(DeviceKind kind, DevicePolicy policy)
        {
            Options.Policies[kind] = policy;
            RecomputeActive(kind, raiseEvents: true);
        }

        /// <summary>
        /// Makes <paramref name="device"/> the current choice for its kind and saves it to
        /// 'configfile.devices'. Pass null to go back to automatic (OS default). The choice survives
        /// the device being unplugged: it is used again as soon as the device reappears.
        /// </summary>
        public void Select(DeviceKind kind, DeviceInfo? device)
        {
            var policy = Options.PolicyFor(kind);
            policy.PreferredName = device?.Id;
            Options.Policies[kind] = policy;

            _config?.SetCurrent(kind, device?.Id);
            RecomputeActive(kind, raiseEvents: true);

            // A running camera keeps its old device until it is reopened.
            if (kind == DeviceKind.Camera && CameraInput.Instance.IsRunning)
                CameraInput.Instance.Reconfigure(CreateCameraOptions());
        }

        /// <summary>
        /// Re-probes cameras. Opens each index briefly, so the privacy light flashes. Does nothing
        /// useful while a camera is running (the probe is skipped and the old list kept).
        /// </summary>
        public void RefreshCameras()
        {
            if (_cameras == null) return;
            if (_cameras.Probe()) Refresh(raiseEvents: true);
        }

        /// <summary>CameraInput options pointing at the primary camera (index 0 when none was found).</summary>
        public CameraInputOptions CreateCameraOptions()
        {
            var options = new CameraInputOptions();
            var camera = Primary(DeviceKind.Camera);
            if (camera != null) options.DeviceIndex = camera.Index;
            return options;
        }

        private bool HasMonitorChoice => !string.IsNullOrWhiteSpace(Options.PolicyFor(DeviceKind.Monitor).PreferredName);

        private void OnActiveDevicesChanged(DeviceKind kind)
        {
            // Also covers the chosen monitor being unplugged: the window follows the fallback monitor
            // instead of being stranded off-screen.
            if (kind == DeviceKind.Monitor && HasMonitorChoice)
                MoveWindowToMonitor();
        }

        /// <summary>
        /// Puts the window on the active monitor, keeping its window mode (centered when windowed, covering
        /// it when borderless or fullscreen). Does nothing if the window is already there.
        /// </summary>
        public void MoveWindowToMonitor()
        {
            var window = Engine.Graphics?.Window;
            var target = Primary(DeviceKind.Monitor);
            if (window == null || target == null) return;

            foreach (var monitor in Silk.NET.Windowing.Monitor.GetMonitors(window))
            {
                if (monitor.Index != target.Index) continue;

                // Graphics knows how each window mode is placed (centered, borderless, exclusive fullscreen).
                Engine.Graphics.PlaceOnMonitor(monitor);

                Console.WriteLine($"[DEVICES] Window placed on {target}");
                return;
            }
        }

        public void LogInventory()
        {
            Console.WriteLine("[DEVICES] Inventory:");
            foreach (var kind in Enum.GetValues<DeviceKind>())
            {
                var connected = _connected[kind];
                var active = _active[kind];
                Console.WriteLine(
                    $"[DEVICES]   {kind}: {connected.Count} connected, {active.Count} active (policy {Options.PolicyFor(kind)})");

                foreach (var device in connected)
                    Console.WriteLine($"[DEVICES]     {(active.Contains(device) ? "*" : " ")} {device}");
            }
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void Refresh(bool raiseEvents)
        {
            var found = new List<DeviceInfo>();

            foreach (var provider in _providers)
            {
                try
                {
                    provider.Enumerate(found);
                }
                catch (Exception ex)
                {
                    // Report each broken provider once instead of every poll.
                    if (_failedProviders.Add(provider.Name))
                        Console.WriteLine($"[DEVICES] Provider '{provider.Name}' failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            foreach (var kind in Enum.GetValues<DeviceKind>())
            {
                var now = found.Where(d => d.Kind == kind).GroupBy(d => d.Id).Select(g => g.First()).ToList();
                var before = _connected[kind];

                var removed = before.Where(b => now.All(n => n.Id != b.Id)).ToList();
                var added = now.Where(n => before.All(b => b.Id != n.Id)).ToList();

                _connected[kind] = now;
                RecomputeActive(kind, raiseEvents);

                if (!raiseEvents) continue;

                foreach (var device in removed)
                {
                    if (Options.LogChanges) Console.WriteLine($"[DEVICES] - {device}");
                    DeviceRemoved?.Invoke(device);
                }

                foreach (var device in added)
                {
                    if (Options.LogChanges) Console.WriteLine($"[DEVICES] + {device}");
                    DeviceAdded?.Invoke(device);
                }
            }
        }

        private void RecomputeActive(DeviceKind kind, bool raiseEvents)
        {
            var policy = Options.PolicyFor(kind);
            var ordered = _connected[kind]
                .OrderByDescending(d => MatchesPreferred(d, policy))
                .ThenByDescending(d => d.IsDefault)
                .ThenBy(d => d.Index)
                .ToList();

            int take = policy.Mode switch
            {
                DeviceMode.Primary => 1,
                DeviceMode.FirstN => Math.Max(0, policy.Max),
                _ => ordered.Count
            };

            var next = ordered.Take(take).ToList();
            bool changed = !_active[kind].Select(d => d.Id).SequenceEqual(next.Select(d => d.Id));

            _active[kind] = next;

            if (changed && raiseEvents)
                ActiveDevicesChanged?.Invoke(kind);
        }

        private static bool MatchesPreferred(DeviceInfo device, DevicePolicy policy)
        {
            if (string.IsNullOrWhiteSpace(policy.PreferredName)) return false;

            return device.Id == policy.PreferredName ||
                   device.Name.Contains(policy.PreferredName, StringComparison.OrdinalIgnoreCase);
        }
    }
}