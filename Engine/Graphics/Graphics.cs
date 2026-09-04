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
        public bool IsFullscreen { get; private set; } = false;

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
                IsVisible = false
            };

            Window = Silk.NET.Windowing.Window.Create(options);
            ConsoleUtils.SetPosition(200, 800);

            // Subscribe to Silk.NET's native window resize event
            Window.Resize += OnResize;

            Batch = new Batch.Batch();
        }

        public void InitializeBatch()
        {
            Batch.Init();
            SystemStats.Initialize();
        }

        public void OnRun()
        {
            Window.Run();
        }

        public void OnLoad()
        {
            //PostProcess = new PostProcessStack(Window.Size.X, Window.Size.Y);
            //var mainPass = new PostProcessPass("shader.post");
            //mainPass.ChromaStrength = 0.01f;
            //PostProcess.Passes.Add(mainPass);

            Window.IsVisible = true;
        }


        public void OnRender(double delta)
        {
            if (!Window.IsVisible || Window.Size.X == 0 || Window.Size.Y == 0)
            {
                return;
            }

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
            // Ignore invalid minimized dimensions
            if (size.X <= 0 || size.Y <= 0) return;

            // 1. Keep track of the updated window dimensions
            WindowSize = size;

            // 2. Adjust OpenGL context viewport
            Batch?.OnResize(size);

            // 3. Update Camera aspect ratio (if active)
            var camera = GetCurrentCamera();
            if (camera != null)
            {
                camera.AspectRatio = (float)size.X / size.Y;
            }

            // (PostProcess left alone per your instruction)
            PostProcess?.Resize(size.X, size.Y);
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


        public void ToggleFullscreen()
        {
            if (!IsFullscreen)
            {
                WindowSize = Window.Size;

                var monitor = Window.Monitor ?? Silk.NET.Windowing.Monitor.GetMainMonitor(Window);
                var monitorSize = monitor.Bounds.Size;

                Window.WindowState = Silk.NET.Windowing.WindowState.Fullscreen;
                Window.Size = monitorSize;
                Window.Position = new Vector2D<int>(0, 0);

                IsFullscreen = true;
            }
            else
            {
                Window.WindowState = Silk.NET.Windowing.WindowState.Normal;
                Window.Size = WindowSize;

                IsFullscreen = false;
            }
        }


        public void SetWindowIsVisible(bool isVisible)
        {
            Window.IsVisible = isVisible;
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
