using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace OssianForge.Engine.Devices.Providers
{
    /// <summary>
    /// Lists the physical keyboards and mice Windows knows about. Silk.NET's GLFW backend always
    /// reports exactly one logical keyboard and one logical mouse, so this is the only way to
    /// answer "how many are plugged in". It is inventory only: input still arrives merged.
    ///
    /// Caveat: Raw Input lists HID interfaces, so one physical device can occasionally show up
    /// twice (some gaming mice and keyboards expose several interfaces).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class RawInputProvider : IDeviceProvider
    {
        private const uint RimTypeMouse = 0;
        private const uint RimTypeKeyboard = 1;
        private const uint RidiDeviceName = 0x20000007;

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDeviceList
        {
            public IntPtr Device;
            public uint Type;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            [In, Out] RawInputDeviceList[]? list, ref uint count, uint size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr device, uint command, StringBuilder? data, ref uint size);

        private static readonly Regex VidPid = new(
            @"VID[_&]([0-9A-F]+)[&_]PID[_&]?([0-9A-F]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Remote-desktop keyboards/mice are listed by Windows but are not hardware.</summary>
        public bool IncludeVirtual = false;

        public string Name => "Windows Raw Input";

        public void Enumerate(List<DeviceInfo> into)
        {
            uint structSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
            uint count = 0;

            if (GetRawInputDeviceList(null, ref count, structSize) == uint.MaxValue || count == 0)
                return;

            var list = new RawInputDeviceList[count];
            uint got = GetRawInputDeviceList(list, ref count, structSize);
            if (got == uint.MaxValue) return;

            int keyboards = 0, mice = 0;
            var seen = new HashSet<string>();

            for (int i = 0; i < got; i++)
            {
                var entry = list[i];
                bool isKeyboard = entry.Type == RimTypeKeyboard;
                if (!isKeyboard && entry.Type != RimTypeMouse) continue;

                string path = ReadDevicePath(entry.Device);
                if (path.Length == 0 || !seen.Add(path)) continue;

                bool isVirtual = path.Contains("RDP_", StringComparison.OrdinalIgnoreCase);
                if (isVirtual && !IncludeVirtual) continue;

                var kind = isKeyboard ? DeviceKind.Keyboard : DeviceKind.Mouse;
                int index = isKeyboard ? keyboards++ : mice++;

                into.Add(new DeviceInfo(kind, $"raw:{path}", Describe(path, isKeyboard), index, index == 0));
            }
        }

        private static string ReadDevicePath(IntPtr handle)
        {
            uint size = 0; // in characters for RIDI_DEVICENAME
            if (GetRawInputDeviceInfo(handle, RidiDeviceName, null, ref size) == uint.MaxValue || size == 0)
                return "";

            var sb = new StringBuilder((int)size);
            if (GetRawInputDeviceInfo(handle, RidiDeviceName, sb, ref size) == uint.MaxValue)
                return "";

            return sb.ToString();
        }

        private static string Describe(string path, bool keyboard)
        {
            string label = keyboard ? "Keyboard" : "Mouse";

            var match = VidPid.Match(path);
            if (match.Success)
                return $"{label} (HID {match.Groups[1].Value}:{match.Groups[2].Value})";

            if (path.Contains("ACPI", StringComparison.OrdinalIgnoreCase))
                return $"{label} (built-in)";

            return $"{label} (other)";
        }
    }
}