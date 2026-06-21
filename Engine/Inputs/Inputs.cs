using Silk.NET.Input;
using Silk.NET.Windowing;

namespace OssianForge.Engine.Inputs
{
    public class Inputs
    {
        public IInputContext InputContext;

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
            InputContext = Engine.Graphics.Window.CreateInput();
            keyboard.Initialize(InputContext.Keyboards[0]);
            mouse.Initialize(InputContext.Mice[0]);
            //mouse.SetCursorMode(CursorMode.Disabled);
            KeyHandler.OnLoad();
        }

        public void OnUpdate(double delta)
        {
            keyboard.Update();
            mouse.Update(delta);
            KeyHandler.OnUpdate();
        }
    }
}