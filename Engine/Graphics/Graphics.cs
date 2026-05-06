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

namespace OssianForge.Engine.Graphics
{
    public class Graphics
    {
        public IWindow Window;
        public GL OpenGL;

        public double CurrentDelta;
        public Vector2D<int> WindowSize;
        public string WindowTitle;
        public Vector2D<int> Resolution;

        public Camera.Camera Camera;

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

            Camera = new Camera.Camera
            {
                Position = new Vector3(0, 1.5f, 3f),  // slightly above
                AspectRatio = (float)WindowSize.X / WindowSize.Y
            };
        }


        public void InitializeOpenGL()
        {
            OpenGL = GL.GetApi(Window);
            OpenGL.Enable(EnableCap.DepthTest);
            OpenGL.Disable(EnableCap.CullFace);
            OpenGL.Enable(EnableCap.Blend);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            OpenGL.Enable(EnableCap.DepthTest);
            OpenGL.DepthFunc(DepthFunction.Less);
            OpenGL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
        }

        public void OnLoad()
        {
            PostProcess = new PostProcessStack(Window.Size.X, Window.Size.Y);
            var mainPass = new PostProcessPass("shaderfile.post.vert", "shaderfile.post.frag");
            //mainPass.ChromaStrength = 0.01f;
            PostProcess.Passes.Add(mainPass);
        }


        public void OnUpdate(double delta)
        {
            Camera.OnUpdate(delta);
        }


        public void OnRender(double delta)
        {
            CurrentDelta = delta;
            OpenGL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            PostProcess.BeginScene();
            Engine.Nodes.OnRender(delta);
            PostProcess.EndScene();
        }

        public void OnResize(Vector2D<int> size)
        {
            OpenGL.Viewport(size);
            PostProcess.Resize(size.X, size.Y);
        }
    }
}
