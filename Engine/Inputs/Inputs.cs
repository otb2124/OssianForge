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
            // The context is owned by Engine.Devices so every module shares one.
            InputContext = Engine.Devices.InputContext!;

            if (InputContext.Keyboards.Count > 0)
                KeyboardInput.Initialize(InputContext.Keyboards[0]);
            else
                Console.WriteLine("[INPUT] No keyboard reported by the windowing backend; keyboard input is disabled.");

            if (InputContext.Mice.Count > 0)
                MouseInput.Initialize(InputContext.Mice[0]);
            else
                Console.WriteLine("[INPUT] No mouse reported by the windowing backend; mouse input is disabled.");
            //MouseInput.SetCursorMode(CursorMode.Disabled);

            CameraInput.Initialize(Engine.Devices.CreateCameraOptions());
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