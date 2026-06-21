using System.Collections.Generic;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Inputs
{
    public class KeyHandler
    {
        public struct InputKey
        {
            public bool IsMouseButton;
            public Silk.NET.Input.Key? KeyboardKey;
            public FlatMouse.MouseButtons? MouseButton;

            public InputKey(Silk.NET.Input.Key key)
            {
                IsMouseButton = false;
                KeyboardKey = key;
                MouseButton = null;
            }

            public InputKey(FlatMouse.MouseButtons button)
            {
                IsMouseButton = true;
                KeyboardKey = null;
                MouseButton = button;
            }
        }

        // Pre-resolved binding, built once in OnLoad — no string parsing at runtime
        private struct ResolvedBinding
        {
            public string Id;
            public InputKeyType Type;
            public List<InputKey> Keys;
        }

        // Pre-resolved axis binding
        private struct ResolvedAxisBinding
        {
            public string Id;
            public string Source;
            public float Sensitivity;
            public bool Invert;
        }

        private readonly List<ResolvedBinding> _bindings = new();
        private readonly List<ResolvedAxisBinding> _axisBindings = new();

        public KeyHandler() { }

        // ── load ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves all InputKeysConfig and InputAxesConfig resource files into
        /// flat bindings. Call once after resources are loaded — not per-frame.
        /// </summary>
        public void OnLoad()
        {
            _bindings.Clear();
            _axisBindings.Clear();

            var keyConfigs = Engine.Resources.GetResourceFiles<InputKeysConfig>();
            foreach (var config in keyConfigs)
            {
                foreach (var record in config.GetAllRecords())
                {
                    _bindings.Add(new ResolvedBinding
                    {
                        Id = record.Id,
                        Type = record.Type,
                        Keys = config.ResolveBindings(record)
                    });
                }
            }

            var axisConfigs = Engine.Resources.GetResourceFiles<InputAxesConfig>();
            foreach (var config in axisConfigs)
            {
                foreach (var record in config.GetAllRecords())
                {
                    _axisBindings.Add(new ResolvedAxisBinding
                    {
                        Id = record.Id,
                        Source = record.Source,
                        Sensitivity = record.Sensitivity,
                        Invert = record.Invert
                    });
                }
            }

            Console.WriteLine($"[KEY HANDLER] Loaded {_bindings.Count} input bindings from {keyConfigs.Count} config(s), "
                + $"{_axisBindings.Count} axis bindings from {axisConfigs.Count} config(s).");
        }

        // ── update ────────────────────────────────────────────────────────────────

        public void OnUpdate()
        {
            foreach (var binding in _bindings)
            {
                bool result = binding.Type switch
                {
                    InputKeyType.Click => AnyClicked(binding.Keys),
                    InputKeyType.Release => AnyReleased(binding.Keys),
                    InputKeyType.Down => AnyDown(binding.Keys),
                    _ => false
                };

                InputStateStore.Set(binding.Id, result);
            }

            foreach (var axis in _axisBindings)
            {
                float raw = ResolveAxisSource(axis.Source);
                float scaled = raw * axis.Sensitivity * (axis.Invert ? -1f : 1f);
                ValueStore.Set(axis.Id, scaled);
            }
        }

        private static float ResolveAxisSource(string source) => source switch
        {
            "MouseDeltaX" => Engine.Inputs.mouse.Delta.X,
            "MouseDeltaY" => Engine.Inputs.mouse.Delta.Y,
            "ScrollDelta" => Engine.Inputs.mouse.ScrollDelta,
            _ => 0f
        };

        private static bool AnyDown(List<InputKey> bindings)
        {
            bool isPressed = false;
            foreach (var b in bindings)
                isPressed |= b.IsMouseButton
                    ? Engine.Inputs.mouse.IsMouseButtonDown(b.MouseButton!.Value)
                    : Engine.Inputs.keyboard.IsKeyDown(b.KeyboardKey!.Value);
            return isPressed;
        }

        private static bool AnyClicked(List<InputKey> bindings)
        {
            bool isClicked = false;
            foreach (var b in bindings)
                isClicked |= b.IsMouseButton
                    ? Engine.Inputs.mouse.IsMouseButtonPressed(b.MouseButton!.Value)
                    : Engine.Inputs.keyboard.IsKeyClicked(b.KeyboardKey!.Value);
            return isClicked;
        }

        private static bool AnyReleased(List<InputKey> bindings)
        {
            bool isReleased = false;
            foreach (var b in bindings)
                isReleased |= b.IsMouseButton
                    ? Engine.Inputs.mouse.IsMouseButtonReleased(b.MouseButton!.Value)
                    : Engine.Inputs.keyboard.IsKeyReleased(b.KeyboardKey!.Value);
            return isReleased;
        }

        public bool IsStateActive(string id) => InputStateStore.IsActive(id);

        public float GetAxis(string id) => ValueStore.Get(id) is float f ? f : 0f;
    }
}