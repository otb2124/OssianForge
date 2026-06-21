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
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Utils.Console;

namespace OssianForge.Engine.Graphics
{
    public class Graphics
    {
        public IWindow Window;

        public Batch.Batch Batch;

        public double CurrentDelta;
        private double _fpsAccum;
        private int _fpsFrameCount;
        private double _smoothFps;

        public double FPS => CurrentDelta > 0 ? 1.0 / CurrentDelta : 0;
        public double SmoothFPS => _smoothFps;
        public int DrawCalls => Batch.DrawCallCount;
        public int RenderedVertices => Batch.VertexCount;
        public Vector2D<int> ViewportSize => WindowSize;
        public double FrameTimeMs => CurrentDelta * 1000.0;


        public Vector2D<int> WindowSize;
        public string WindowTitle;
        public Vector2D<int> Resolution;
        public int TargetFramesPerSecond = 120;
        public int TargetUpdatesPerSecond = 120;

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
            WindowOptions options = WindowOptions.Default with
            {
                Size = WindowSize,
                Title = WindowTitle,
                FramesPerSecond = TargetFramesPerSecond,
                UpdatesPerSecond = TargetUpdatesPerSecond,
            };

            Window = Silk.NET.Windowing.Window.Create(options);
            ConsoleUtils.SetPosition(200, 800);

            Batch = new Batch.Batch();
        }

        public void InitializeBatch()
        {
            Batch.Init();
            SystemStats.Initialize();
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
            UpdateRenderData(delta);

            Batch.Clear();
            Engine.Nodes.OnRender(delta);
        }

        public void UpdateRenderData(double delta)
        {
            CurrentDelta = delta;

            _fpsAccum += delta;
            _fpsFrameCount++;
            if (_fpsAccum >= 0.5)
            {
                _smoothFps = _fpsFrameCount / _fpsAccum;
                _fpsAccum = 0;
                _fpsFrameCount = 0;
            }
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

    public static class GraphicsStats
    {
        public static string GetFPS() => Engine.Graphics.FPS.ToString("F1");
        public static string GetSmoothFPS() => Engine.Graphics.SmoothFPS.ToString("F1");
        public static string GetFrameTimeMs() => Engine.Graphics.FrameTimeMs.ToString("F2");
        public static string GetDrawCalls() => Engine.Graphics.Batch.DrawCallCount.ToString();
        public static string GetVertexCount() => Engine.Graphics.Batch.VertexCount.ToString();
        public static string GetResolution() => $"{Engine.Graphics.WindowSize.X}x{Engine.Graphics.WindowSize.Y}";
    }
}
