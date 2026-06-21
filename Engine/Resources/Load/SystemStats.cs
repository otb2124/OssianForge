using Silk.NET.OpenGL;
using System.Diagnostics;

namespace OssianForge.Engine.Graphics
{
    public static class SystemStats
    {
        private static readonly Process _process = Process.GetCurrentProcess();
        private static TimeSpan _lastCpuTime = TimeSpan.Zero;
        private static DateTime _lastCpuCheck = DateTime.UtcNow;
        private static float _cpuUsage;
        private static bool _initialized;

        // ── GPU device info (queried once) ───────────────────────────────────────
        public static string GpuVendor { get; private set; } = "";
        public static string GpuRenderer { get; private set; } = "";
        public static string GpuVersion { get; private set; } = "";
        public static string GlslVersion { get; private set; } = "";

        // ── per-frame stats ──────────────────────────────────────────────────────
        public static float CpuUsagePercent => _cpuUsage;
        public static long MemoryUsageMb => _process.WorkingSet64 / (1024 * 1024);
        public static long ManagedMemoryMb => GC.GetTotalMemory(false) / (1024 * 1024);
        public static int GcGen0 => GC.CollectionCount(0);
        public static int GcGen1 => GC.CollectionCount(1);
        public static int GcGen2 => GC.CollectionCount(2);

        // ── init (call once after OpenGL is ready) ───────────────────────────────
        public static void Initialize()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            GpuVendor = gl.GetStringS(StringName.Vendor);
            GpuRenderer = gl.GetStringS(StringName.Renderer);
            GpuVersion = gl.GetStringS(StringName.Version);
            GlslVersion = gl.GetStringS(StringName.ShadingLanguageVersion);
            Console.WriteLine($"[SYSTEM STATS] GPU: {GpuRenderer} | {GpuVersion}");

            // Baseline here so the first Update() diffs against a real value
            // instead of TimeSpan.Zero (which caused a bogus spike on frame 1).
            _process.Refresh();
            _lastCpuTime = _process.TotalProcessorTime;
            _lastCpuCheck = DateTime.UtcNow;
            _initialized = true;
        }

        // ── update (call once per frame) ─────────────────────────────────────────
        public static void Update()
        {
            if (!_initialized)
            {
                // Safety net if Update() ever runs before Initialize().
                _process.Refresh();
                _lastCpuTime = _process.TotalProcessorTime;
                _lastCpuCheck = DateTime.UtcNow;
                _initialized = true;
                return;
            }

            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCpuCheck).TotalSeconds;
            if (elapsed < 0.5) return;

            // Process caches its snapshot on first read. Without Refresh(),
            // TotalProcessorTime (and WorkingSet64 / MemoryUsageMb) never
            // change again, so cpuDelta stays ~0 forever → stuck at 0%.
            _process.Refresh();

            var cpuTime = _process.TotalProcessorTime;
            var cpuDelta = (cpuTime - _lastCpuTime).TotalSeconds;
            _cpuUsage = (float)(cpuDelta / (elapsed * Environment.ProcessorCount) * 100.0);

            _lastCpuTime = cpuTime;
            _lastCpuCheck = now;
        }

        // ── string getters (for actions) ─────────────────────────────────────────
        public static string GetGpuVendor() => GpuVendor;
        public static string GetGpuRenderer() => GpuRenderer;
        public static string GetGpuVersion() => GpuVersion;
        public static string GetGlslVersion() => GlslVersion;
        public static string GetCpuUsage() => $"{CpuUsagePercent:F1}%";
        public static string GetMemoryUsage() => $"{MemoryUsageMb} MB";
        public static string GetManagedMemory() => $"{ManagedMemoryMb} MB";
        public static string GetGcInfo() => $"G0:{GcGen0} G1:{GcGen1} G2:{GcGen2}";
        public static string GetProcessorCount() => Environment.ProcessorCount.ToString();
        public static string GetOsVersion() => Environment.OSVersion.ToString();
    }
}