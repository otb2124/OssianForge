using Silk.NET.Input;
using System.Collections.Generic;

namespace OssianForge.Engine.Inputs
{
    public class KeyHandler
    {

        public struct InputKey
        {
            public bool IsMouseButton;
            public Key? KeyboardKey;
            public FlatMouse.MouseButtons? MouseButton;

            public InputKey(Key key)
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

        public enum KeyStates
        {
            //Player keys
            JUMPPRESSED,
            MOVERIGHTPRESSED,
            MOVELEFTPRESSED,
            MOVEDOWNPRESSED,
            MOVEUPPRESSED,
            ATTACKLIGHTPRESSED,
            ATTACKHEAVYPRESSED,
            TOGGLEWEAPONPRESSED,
            SPRINTPRESSED,
            INTERACTRESSED,
            PARRYPRESSED,
            BLOCKPRESSED,

            //Camera
            CAMERALEFTPRESSED,
            CAMERARIGHTPRESSED,
            CAMERAUPPRESSED,
            CAMERADOWNPRESSED,

            CAMERAZOOMUPPRESSED,
            CAMERAZOOMDOWNPRESSED,

            //UI
            TOGGLEMENUPRESSED,
            TOGGLEHUDPRESSED,

            //debug
            TOGGLECOLLISIONDEBUGPRESSED,
            TOGGLEHITBOXDEBUGPRESSED
        }

        private Dictionary<KeyStates, bool> ActiveStates = new();


        public Dictionary<(KeyStates state, bool clickOnly), List<InputKey>> KeyBindings = new Dictionary<(KeyStates, bool), List<InputKey>>
        {
            //Player keys
            { (KeyStates.MOVERIGHTPRESSED, false), new List<InputKey> { new InputKey(Key.D) } },
            { (KeyStates.MOVELEFTPRESSED, false), new List<InputKey> { new InputKey(Key.A) } },
            { (KeyStates.MOVEDOWNPRESSED, false), new List<InputKey> { new InputKey(Key.S) } },
            { (KeyStates.MOVEUPPRESSED, false), new List<InputKey> { new InputKey(Key.W) } },
            { (KeyStates.SPRINTPRESSED, false), new List<InputKey> { new InputKey(Key.ShiftLeft) } },
            { (KeyStates.JUMPPRESSED, false), new List<InputKey> { new InputKey(Key.Space) } },
            { (KeyStates.INTERACTRESSED, true), new List<InputKey> { new InputKey(Key.E) } },
            { (KeyStates.BLOCKPRESSED, false), new List<InputKey> { new InputKey(Key.AltLeft) } },
            { (KeyStates.TOGGLEWEAPONPRESSED, true), new List<InputKey> { new InputKey(Key.R),  new InputKey(Key.CapsLock) } },
            { (KeyStates.PARRYPRESSED, false), new List<InputKey> { new InputKey(Key.ControlLeft) } },
            { (KeyStates.ATTACKLIGHTPRESSED, true), new List<InputKey> { new InputKey(FlatMouse.MouseButtons.Left) } },
            { (KeyStates.ATTACKHEAVYPRESSED, true), new List<InputKey> { new InputKey(FlatMouse.MouseButtons.Right) } },

            //Camera
            { (KeyStates.CAMERALEFTPRESSED, false), new List<InputKey> { new InputKey(Key.Left) } },
            { (KeyStates.CAMERARIGHTPRESSED, false), new List<InputKey> { new InputKey(Key.Right) } },
            { (KeyStates.CAMERAUPPRESSED, false), new List<InputKey> { new InputKey(Key.Up) } },
            { (KeyStates.CAMERADOWNPRESSED, false), new List<InputKey> { new InputKey(Key.Down) } },
            { (KeyStates.CAMERAZOOMUPPRESSED, false), new List<InputKey> { new InputKey(Key.Equal) } },
            { (KeyStates.CAMERAZOOMDOWNPRESSED, false), new List<InputKey> { new InputKey(Key.Minus) } },

            //ui
            { (KeyStates.TOGGLEMENUPRESSED, true), new List<InputKey> { new InputKey(Key.Escape) } },
            { (KeyStates.TOGGLEHUDPRESSED, true), new List<InputKey> { new InputKey(Key.F1) } },

            //debug
            { (KeyStates.TOGGLECOLLISIONDEBUGPRESSED, true), new List<InputKey> { new InputKey(Key.F3) } },
            { (KeyStates.TOGGLEHITBOXDEBUGPRESSED, true), new List<InputKey> { new InputKey(Key.F4) } },
        };


        public KeyHandler() {
        }

        public void Update()
        {
            HandleKeyClicks();
            HandleKeyPresses();
            HandleKeyReleases();
        }

        private void HandleKeyClicks()
        {
            foreach (var ((state, clickOnly), bindings) in KeyBindings)
            {
                if (!clickOnly) continue;
                bool isPressed = false;
                foreach (var binding in bindings)
                {
                    if (binding.IsMouseButton)
                        isPressed |= Engine.Inputs.mouse.IsMouseButtonPressed(binding.MouseButton!.Value);
                    else
                        isPressed |= Engine.Inputs.keyboard.IsKeyClicked(binding.KeyboardKey!.Value);
                }
                SetState(state, isPressed); // ← uncomment
            }
        }

        private void HandleKeyPresses()
        {
            foreach (var ((state, clickOnly), bindings) in KeyBindings)
            {
                if (clickOnly) continue;
                bool isPressed = false;
                foreach (var binding in bindings)
                {
                    if (binding.IsMouseButton)
                        isPressed |= Engine.Inputs.mouse.IsMouseButtonDown(binding.MouseButton!.Value);
                    else
                        isPressed |= Engine.Inputs.keyboard.IsKeyDown(binding.KeyboardKey!.Value);
                }
                SetState(state, isPressed); // ← uncomment
            }
        }

        private void HandleKeyReleases()
        {
            foreach (var ((state, clickOnly), bindings) in KeyBindings)
            {
                if (clickOnly) continue;
                bool allReleased = true;
                foreach (var binding in bindings)
                {
                    bool isReleased = binding.IsMouseButton
                        ? Engine.Inputs.mouse.IsMouseButtonReleased(binding.MouseButton!.Value)
                        : Engine.Inputs.keyboard.IsKeyReleased(binding.KeyboardKey!.Value);
                    allReleased &= isReleased;
                }
                if (allReleased)
                    SetState(state, false); // ← uncomment
            }
        }

        public bool IsStateActive(KeyStates state) =>
            ActiveStates.TryGetValue(state, out bool val) && val;

        private void SetState(KeyStates state, bool value) =>
            ActiveStates[state] = value;


        private bool IsAnyPressed()
        {
            return Engine.Inputs.keyboard.GetPressedKeys().Count > 0 || Engine.Inputs.mouse.GetPressedButtons().Count > 0;
        }
    }
}
