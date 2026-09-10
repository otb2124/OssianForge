using Silk.NET.Input;
using System.Numerics;

namespace OssianForge.Engine.Inputs
{
    public sealed class MouseInput
    {
        public enum MouseButtons { Left, Right, Middle }
        private static Lazy<MouseInput> LazyInstance = new(() => new MouseInput());
        public static MouseInput Instance => LazyInstance.Value;
        private IMouse _mouse;
        private HashSet<MouseButton> _curr = new();
        private HashSet<MouseButton> _prev = new();
        private float _prevScroll;
        private Vector2 _prevPosition;
        private bool _firstUpdate = true;

        public Vector2 Position => _mouse?.Position ?? Vector2.Zero;
        public Vector2 Delta { get; private set; }   // ← new: frame-to-frame movement
        private double _lastDeltaTime;
        public float ScrollDelta { get; private set; }
        public bool LockCursorToCenter { get; set; } = true;
        public bool IsFocused { get; set; } = true;

        private MouseInput() { }

        public void Initialize(IMouse mouse)
        {
            _mouse = mouse;
        }

        public void Update(double deltaTime)
        {
            _prev = new HashSet<MouseButton>(_curr);
            _curr.Clear();
            foreach (MouseButton btn in Enum.GetValues<MouseButton>())
            {
                if (_mouse.IsButtonPressed(btn))
                    _curr.Add(btn);
            }

            float scroll = _mouse.ScrollWheels.Count > 0 ? _mouse.ScrollWheels[0].Y : 0f;
            ScrollDelta = scroll - _prevScroll;
            _prevScroll = scroll;

            Vector2 currentPos = Position;
            // Normalize to pixels/second so consumers can multiply by $delta
            float dt = (float)Math.Max(deltaTime, 0.0001);
            Delta = (_firstUpdate || !IsFocused) ? Vector2.Zero : new Vector2(
                -(currentPos.X - _prevPosition.X),
                 (currentPos.Y - _prevPosition.Y)) / dt;

            if (LockCursorToCenter && IsFocused && _mouse != null)
            {
                var windowSize = Engine.Graphics.WindowSize;
                Vector2 center = new Vector2(windowSize.X / 2f, windowSize.Y / 2f);
                _mouse.Position = center;
                _prevPosition = center;
            }
            else
            {
                _prevPosition = currentPos;
            }

            _firstUpdate = false;
        }

        private MouseButton ToSilk(MouseButtons btn) => btn switch
        {
            MouseButtons.Left => MouseButton.Left,
            MouseButtons.Right => MouseButton.Right,
            MouseButtons.Middle => MouseButton.Middle,
            _ => MouseButton.Left
        };

        public bool IsMouseButtonDown(MouseButtons btn) => _curr.Contains(ToSilk(btn));
        public bool IsMouseButtonPressed(MouseButtons btn) => _curr.Contains(ToSilk(btn)) && !_prev.Contains(ToSilk(btn));
        public bool IsMouseButtonReleased(MouseButtons btn) => !_curr.Contains(ToSilk(btn)) && _prev.Contains(ToSilk(btn));

        public List<MouseButtons> GetPressedButtons()
        {
            return Enum.GetValues<MouseButtons>()
                .Where(IsMouseButtonDown)
                .ToList();
        }

        public void SetCursorMode(CursorMode mode)
        {
            if (_mouse?.Cursor != null && _mouse.Cursor.IsSupported(mode))
            {
                _mouse.Cursor.CursorMode = mode;
            }
        }

        public void SetFocused(bool focused)
        {
            IsFocused = focused;
        }
    }
}