using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Devices.Providers
{
    /// <summary>Monitors as GLFW/Silk reports them. Needs the window, so it is registered in Devices.OnLoad.</summary>
    public sealed class MonitorProvider : IDeviceProvider
    {
        public string Name => "Silk.NET monitors";

        public void Enumerate(List<DeviceInfo> into)
        {
            var window = Engine.Graphics?.Window;
            if (window == null) return;

            int mainIndex = -1;
            try { mainIndex = Silk.NET.Windowing.Monitor.GetMainMonitor(window).Index; }
            catch { /* no main monitor reported; nothing is marked default */ }

            foreach (var monitor in Silk.NET.Windowing.Monitor.GetMonitors(window))
            {
                var bounds = monitor.Bounds;
                string name = string.IsNullOrWhiteSpace(monitor.Name) ? $"Monitor {monitor.Index}" : monitor.Name;

                into.Add(new DeviceInfo(
                    DeviceKind.Monitor,
                    $"monitor:{monitor.Index}",
                    name,
                    monitor.Index,
                    monitor.Index == mainIndex,
                    $"{bounds.Size.X}x{bounds.Size.Y} at ({bounds.Origin.X},{bounds.Origin.Y})"));
            }
        }
    }
}