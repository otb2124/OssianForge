using Silk.NET.Input;
using System.Numerics;

namespace OssianForge.Engine.Inputs
{
    public sealed class FlatMouse
    {
        public enum MouseButtons { Left, Right, Middle }
        private static Lazy<FlatMouse> LazyInstance = new(() => new FlatMouse());
        public static FlatMouse Instance => LazyInstance.Value;
        private IMouse _mouse;
        private HashSet<MouseButton> _curr = new();
        private HashSet<MouseButton> _prev = new();
        private float _prevScroll;
        private Vector2 _prevPosition;
        private bool _firstUpdate = true;

        public Vector2 Position => _mouse?.Position ?? Vector2.Zero;
        public Vector2 Delta { get; private set; }   // ← new: frame-to-frame movement
        public float ScrollDelta { get; private set; }

        private FlatMouse() { }

        public void Initialize(IMouse mouse)
        {
            _mouse = mouse;
        }

        public void Update()
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
            Delta = _firstUpdate ? Vector2.Zero : currentPos - _prevPosition;
            _prevPosition = currentPos;
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
    }
}