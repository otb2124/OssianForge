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

        public Vector2 Position => _mouse?.Position ?? Vector2.Zero;
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
    }
}