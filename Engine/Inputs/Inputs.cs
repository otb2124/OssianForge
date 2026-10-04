using Silk.NET.Input;
using Silk.NET.Windowing;

namespace OssianForge.Engine.Inputs
{
    public class Inputs
    {
        public IInputContext InputContext;

        public KeyboardInput KeyboardInput;
        public MouseInput MouseInput;
        public MicrophoneInput MicrophoneInput;
        public CameraInput CameraInput;

        public KeyHandler KeyHandler;

        public void Initialize()
        {
            KeyboardInput = KeyboardInput.Instance;
            MouseInput = MouseInput.Instance;
            MicrophoneInput = MicrophoneInput.Instance;
            CameraInput = CameraInput.Instance;

            KeyHandler = new KeyHandler();
        }

        public void OnLoad()
        {
            InputContext = Engine.Graphics.Window.CreateInput();
            KeyboardInput.Initialize(InputContext.Keyboards[0]);
            MouseInput.Initialize(InputContext.Mice[0]);
            //MouseInput.SetCursorMode(CursorMode.Disabled);

            CameraInput.Initialize(new CameraInputOptions());
            MicrophoneInput.Initialize(new MicrophoneInputOptions());
            KeyHandler.OnLoad();
        }

        public void OnUpdate(double delta)
        {
            KeyboardInput.Update();
            MouseInput.Update(delta);
            CameraInput.Update();
            MicrophoneInput.Update(delta);

            KeyHandler.OnUpdate();
        }

        public void OnFocusChanged(bool focused)
        {
            MouseInput.SetFocused(focused);
        }

        public void Shutdown()
        {
            CameraInput.Shutdown();
            MicrophoneInput.Shutdown();
        }
    }
}