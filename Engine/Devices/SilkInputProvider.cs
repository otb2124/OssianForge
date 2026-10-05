using Silk.NET.Input;

namespace OssianForge.Engine.Devices.Providers
{
    /// <summary>
    /// What Silk.NET itself reports. Gamepads are listed individually. Keyboards and mice are only
    /// listed when <c>includeKeyboardsAndMice</c> is set (non-Windows platforms), because on GLFW
    /// Silk always shows exactly one of each regardless of hardware.
    /// </summary>
    public sealed class SilkInputProvider : IDeviceProvider
    {
        private readonly Func<IInputContext?> _context;
        private readonly bool _keyboardsAndMice;

        public string Name => "Silk.NET input";

        public SilkInputProvider(Func<IInputContext?> context, bool includeKeyboardsAndMice)
        {
            _context = context;
            _keyboardsAndMice = includeKeyboardsAndMice;
        }

        public void Enumerate(List<DeviceInfo> into)
        {
            var context = _context();
            if (context == null) return;

            if (_keyboardsAndMice)
            {
                foreach (var keyboard in context.Keyboards)
                {
                    if (!keyboard.IsConnected) continue;
                    into.Add(new DeviceInfo(DeviceKind.Keyboard, $"silk-keyboard:{keyboard.Index}",
                        keyboard.Name, keyboard.Index, keyboard.Index == 0));
                }

                foreach (var mouse in context.Mice)
                {
                    if (!mouse.IsConnected) continue;
                    into.Add(new DeviceInfo(DeviceKind.Mouse, $"silk-mouse:{mouse.Index}",
                        mouse.Name, mouse.Index, mouse.Index == 0));
                }
            }

            foreach (var gamepad in context.Gamepads)
            {
                if (!gamepad.IsConnected) continue;
                into.Add(new DeviceInfo(DeviceKind.GamePad, $"silk-gamepad:{gamepad.Index}",
                    gamepad.Name, gamepad.Index, gamepad.Index == 0));
            }
        }
    }
}