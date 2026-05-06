using Silk.NET.Input;
using Silk.NET.GLFW;

namespace OssianForge.Engine.Inputs
{
    public sealed class FlatKeyboard
    {
        private static Lazy<FlatKeyboard> LazyInstance = new(() => new FlatKeyboard());
        public static FlatKeyboard Instance => LazyInstance.Value;

        private IKeyboard _keyboard;
        private HashSet<Key> _curr = new();
        private HashSet<Key> _prev = new();

        public bool IsKeyAvailable => _curr.Count > 0;

        private FlatKeyboard() { }

        public void Initialize(IKeyboard keyboard)
        {
            _keyboard = keyboard;
        }

        public void Update()
        {
            _prev = new HashSet<Key>(_curr);
            _curr.Clear();
            foreach (Key key in Enum.GetValues<Key>())
            {
                if (key != Key.Unknown && _keyboard.IsKeyPressed(key))
                    _curr.Add(key);
            }
        }

        public bool IsKeyDown(Key key) => _curr.Contains(key);
        public bool IsKeyClicked(Key key) => _curr.Contains(key) && !_prev.Contains(key);
        public bool IsKeyReleased(Key key) => !_curr.Contains(key) && _prev.Contains(key);

        public List<Key> GetPressedKeys() => _curr.ToList();
    }
}