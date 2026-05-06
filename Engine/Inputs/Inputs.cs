using Silk.NET.Input;
using Silk.NET.Windowing;

namespace OssianForge.Engine.Inputs
{
    public class Inputs
    {
        public FlatKeyboard keyboard;
        public FlatMouse mouse;
        public KeyHandler KeyHandler;

        public void Initialize()
        {
            keyboard = FlatKeyboard.Instance;
            mouse = FlatMouse.Instance;
            KeyHandler = new KeyHandler();
        }

        public void OnLoad()
        {
            var input = Engine.Graphics.Window.CreateInput();
            keyboard.Initialize(input.Keyboards[0]);
            mouse.Initialize(input.Mice[0]);
        }

        public void OnUpdate(double delta)
        {
            keyboard.Update();
            mouse.Update();
            KeyHandler.Update();
        }
    }
}