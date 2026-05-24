using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using OssianForge.Engine.Graphics.Camera;
using OssianForge.Engine.Graphics.RenderTarget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OssianForge.Engine.Graphics.Batch;
using Silk.NET.Input;
using OssianForge.Engine.Graphics.Console;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Graphics
{
    public class Graphics
    {
        public IWindow Window;

        public Batch.Batch Batch;

        public double CurrentDelta;
        public Vector2D<int> WindowSize;
        public string WindowTitle;
        public Vector2D<int> Resolution;

        public string CurrentCameraNode;

        public PostProcessStack PostProcess;
        

        public Graphics()
        {
            WindowSize = new Vector2D<int>(1280, 720);
            WindowTitle = "OssianForge";
            Resolution = new Vector2D<int>(1280, 720);
        }

        public void Initialize()
        {
            var options = WindowOptions.Default with
            {
                Size = WindowSize,
                Title = WindowTitle
            };

            Window = Silk.NET.Windowing.Window.Create(options);
            ConsoleUtils.SetPosition(200, 800);

            Batch = new Batch.Batch();
        }

        public void InitializeBatch()
        {
            Batch.Init();
        }


        public void OnLoad()
        {
            //PostProcess = new PostProcessStack(Window.Size.X, Window.Size.Y);
            //var mainPass = new PostProcessPass("shader.post");
            //mainPass.ChromaStrength = 0.01f;
            //PostProcess.Passes.Add(mainPass);
        }


        public void OnRender(double delta)
        {
            CurrentDelta = delta;
            Batch.Clear();

            //PostProcess.BeginScene();
            Engine.Nodes.OnRender(delta);
            //PostProcess.EndScene();
        }

        public void OnResize(Vector2D<int> size)
        {
            Batch.OnResize(size);
            PostProcess.Resize(size.X, size.Y);
        }

        public Camera.Camera GetCurrentCamera()
        {
            var cameraNode = Engine.Nodes.NodeManager.GetNodeWithProperty<CameraProperty>();

            if (cameraNode == null)
                return null;

            if(cameraNode.Id == CurrentCameraNode)
            {
                return cameraNode.GetProperty<CameraProperty>().Camera;
            }
            else
            {
                CurrentCameraNode = cameraNode.Id;
                return cameraNode.GetProperty<CameraProperty>().Camera;
            }

        }
    }
}
