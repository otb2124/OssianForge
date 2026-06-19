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

        private readonly List<ResolvedBinding> _bindings = new();

        public KeyHandler() { }

        // ── load ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves all InputKeysConfig resource files into flat bindings.
        /// Call once after resources are loaded — not per-frame.
        /// </summary>
        public void OnLoad()
        {
            _bindings.Clear();

            var configs = Engine.Resources.GetResourceFiles<InputKeysConfig>();
            foreach (var config in configs)
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

            Console.WriteLine($"[KEY HANDLER] Loaded {_bindings.Count} input bindings from {configs.Count} config(s).");
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
        }

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
    }
}