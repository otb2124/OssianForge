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
using SilkMonitor = Silk.NET.Windowing.Monitor;
using SilkWindowBorder = Silk.NET.Windowing.WindowBorder;
using SilkWindowState = Silk.NET.Windowing.WindowState;

namespace OssianForge.Engine.Graphics
{
    public enum WindowMode
    {
        /// <summary>Normal framed window. Remembers its size and position.</summary>
        Windowed,

        /// <summary>Frameless window covering the whole monitor ("fullscreen windowed"). No video mode
        /// change, so it is instant to enter and leave and alt-tab friendly. The recommended fullscreen.</summary>
        Borderless,

        /// <summary>Exclusive fullscreen on the monitor's current video mode.</summary>
        Fullscreen
    }

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

        // ── Window mode ──────────────────────────────────────────────────────

        public WindowMode CurrentWindowMode { get; private set; } = WindowMode.Windowed;

        /// <summary>What ToggleFullscreen() switches to from windowed.</summary>
        public WindowMode FullscreenMode = WindowMode.Borderless;

        /// <summary>Mode applied once the window exists (in OnLoad).</summary>
        public WindowMode StartupWindowMode = WindowMode.Fullscreen;

        /// <summary>Size a windowed window gets when it has no remembered size (e.g. the game started fullscreen).</summary>
        public Vector2D<int> DefaultWindowedSize = new(1280, 720);

        /// <summary>True for both borderless and exclusive fullscreen: the window covers the monitor.</summary>
        public bool IsFullscreen => CurrentWindowMode != WindowMode.Windowed;
        public bool IsBorderless => CurrentWindowMode == WindowMode.Borderless;

        // The windowed layout to return to. Kept apart from WindowSize, which always tracks the live size.
        private Vector2D<int> _windowedSize;
        private Vector2D<int> _windowedPosition;
        private SilkWindowBorder _windowedBorder = SilkWindowBorder.Resizable;
        private bool _hasWindowedPlacement;

        // Monitor bookkeeping. Silk's Window.Monitor can't be trusted to say where the window is or where
        // fullscreen will land, so the engine tracks both itself.
        private int _preferredMonitorIndex = -1;  // set by PlaceOnMonitor (Devices' monitor choice)
        private int _exclusiveMonitorIndex = -1;  // monitor the window is exclusively fullscreen on, or -1

        public Vector2D<int> WindowSize;
        public string WindowTitle;
        public Vector2D<int> Resolution;
        public int TargetFramesPerSecond = 120;
        public int TargetUpdatesPerSecond = 120;

        public string CurrentCameraNode;

        public ParallaxController ParallaxController;

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
            ParallaxController = new ParallaxController();
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
            ParallaxController.Enabled = false;
            //ParallaxController.Start();

            if (StartupWindowMode != WindowMode.Windowed)
                SetWindowMode(StartupWindowMode);
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

            ParallaxController.Update(delta);
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

            if (cameraNode.Id == CurrentCameraNode)
            {
                return cameraNode.GetProperty<CameraProperty>().Camera;
            }
            else
            {
                CurrentCameraNode = cameraNode.Id;
                return cameraNode.GetProperty<CameraProperty>().Camera;
            }

        }


        // ── Window mode switching ────────────────────────────────────────────

        /// <summary>
        /// Switches between windowed, borderless and exclusive fullscreen on the active monitor
        /// (the one chosen through Devices, otherwise the one the window is on).
        /// Callable from JSON actions: "borderless", "fullscreen" and "windowed" all coerce to the enum.
        /// </summary>
        public void SetWindowMode(WindowMode mode)
        {
            if (Window == null || mode == CurrentWindowMode) return;

            var monitor = GetCurrentMonitor();

            // Remember the framed layout before leaving it (borderless <-> fullscreen keeps the saved one).
            if (CurrentWindowMode == WindowMode.Windowed)
                SaveWindowedPlacement();

            CurrentWindowMode = ApplyWindowMode(mode, monitor);
            AfterWindowChanged();

            Console.WriteLine($"[GRAPHICS] Window mode: {CurrentWindowMode} (monitor {monitor.Index}, {Window.Size.X}x{Window.Size.Y})");
        }

        /// <summary>Switches between windowed and FullscreenMode (borderless unless changed).</summary>
        public void ToggleFullscreen()
        {
            if (IsFullscreen)
            {
                SetWindowMode(WindowMode.Windowed);
                return;
            }

            SetWindowMode(FullscreenMode == WindowMode.Windowed ? WindowMode.Borderless : FullscreenMode);
        }

        /// <summary>Windowed -> Borderless -> Fullscreen -> Windowed. Handy on a debug key.</summary>
        public void CycleWindowMode()
        {
            SetWindowMode(CurrentWindowMode switch
            {
                WindowMode.Windowed => WindowMode.Borderless,
                WindowMode.Borderless => WindowMode.Fullscreen,
                _ => WindowMode.Windowed
            });
        }

        /// <summary>
        /// Makes a monitor the active one and moves the window there, keeping the current mode: centered when
        /// windowed, covering it when borderless or fullscreen. Does nothing if it is already there.
        /// Later mode switches happen on this monitor until another one is chosen.
        /// </summary>
        public void PlaceOnMonitor(IMonitor monitor)
        {
            if (Window == null || monitor == null) return;

            _preferredMonitorIndex = monitor.Index;
            var bounds = monitor.Bounds;

            switch (CurrentWindowMode)
            {
                case WindowMode.Windowed:
                    {
                        var size = Window.Size;
                        Window.Position = new Vector2D<int>(
                            bounds.Origin.X + (bounds.Size.X - size.X) / 2,
                            bounds.Origin.Y + (bounds.Size.Y - size.Y) / 2);
                        break;
                    }

                case WindowMode.Borderless:
                    if (Window.Position == bounds.Origin && Window.Size == bounds.Size) return;
                    ApplyWindowMode(WindowMode.Borderless, monitor);
                    break;

                case WindowMode.Fullscreen:
                    if (_exclusiveMonitorIndex == monitor.Index) return;
                    CurrentWindowMode = ApplyWindowMode(WindowMode.Fullscreen, monitor);
                    break;
            }

            AfterWindowChanged();
        }

        /// <summary>
        /// The monitor mode switches should happen on, in order of trust: where exclusive fullscreen really is,
        /// the monitor chosen through PlaceOnMonitor, the monitor under the window's centre, Silk's guess.
        /// </summary>
        private IMonitor GetCurrentMonitor()
        {
            var monitors = SilkMonitor.GetMonitors(Window).ToList();

            if (_exclusiveMonitorIndex >= 0)
            {
                var exclusive = monitors.FirstOrDefault(m => m.Index == _exclusiveMonitorIndex);
                if (exclusive != null) return exclusive;
            }

            if (_preferredMonitorIndex >= 0)
            {
                var preferred = monitors.FirstOrDefault(m => m.Index == _preferredMonitorIndex);
                if (preferred != null) return preferred;
            }

            var size = Window.Size;
            var position = Window.Position;
            int centerX = position.X + size.X / 2;
            int centerY = position.Y + size.Y / 2;

            foreach (var monitor in monitors)
            {
                var b = monitor.Bounds;
                if (centerX >= b.Origin.X && centerX < b.Origin.X + b.Size.X &&
                    centerY >= b.Origin.Y && centerY < b.Origin.Y + b.Size.Y)
                    return monitor;
            }

            return Window.Monitor ?? SilkMonitor.GetMainMonitor(Window);
        }

        private void SaveWindowedPlacement()
        {
            // A maximized window reports its maximized size; go back to normal so we store the real layout.
            if (Window.WindowState != SilkWindowState.Normal)
                Window.WindowState = SilkWindowState.Normal;

            _windowedSize = Window.Size;
            _windowedPosition = Window.Position;
            _windowedBorder = Window.WindowBorder;
            _hasWindowedPlacement = true;
        }

        /// <summary>
        /// Applies a mode on a monitor and returns the mode that actually took effect (exclusive fullscreen
        /// falls back to borderless if GLFW can't be reached). Order matters: leave exclusive fullscreen or
        /// maximized first, because size and border changes are ignored while the window is in those states.
        /// </summary>
        private WindowMode ApplyWindowMode(WindowMode mode, IMonitor monitor)
        {
            var bounds = monitor.Bounds;

            if (mode == WindowMode.Fullscreen)
            {
                // Straight to the target monitor, also when already fullscreen on another one.
                if (EnterExclusiveFullscreen(monitor))
                {
                    _exclusiveMonitorIndex = monitor.Index;
                    return WindowMode.Fullscreen;
                }

                Console.WriteLine("[GRAPHICS] Exclusive fullscreen unavailable, using borderless instead.");
                mode = WindowMode.Borderless;
            }

            if (_exclusiveMonitorIndex >= 0)
            {
                // Leave to the rectangle the new mode wants so the window doesn't flash at the wrong place.
                if (mode == WindowMode.Windowed)
                {
                    var (size, position) = GetWindowedPlacement(monitor);
                    LeaveExclusiveFullscreen(position, size);
                }
                else
                {
                    LeaveExclusiveFullscreen(bounds.Origin, bounds.Size);
                }
            }

            if (Window.WindowState != SilkWindowState.Normal)
                Window.WindowState = SilkWindowState.Normal;

            switch (mode)
            {
                case WindowMode.Windowed:
                    {
                        var (size, position) = GetWindowedPlacement(monitor);
                        Window.WindowBorder = _windowedBorder;
                        Window.Size = size;      // set again after the border change, which can resize the client area
                        Window.Position = position;
                        break;
                    }

                case WindowMode.Borderless:
                    Window.WindowBorder = SilkWindowBorder.Hidden;
                    Window.Position = bounds.Origin;
                    Window.Size = bounds.Size;
                    break;
            }

            return mode;
        }

        // ── Exclusive fullscreen ─────────────────────────────────────────────
        // Silk's WindowState = Fullscreen always picks the main monitor, whatever the window position is,
        // so exclusive fullscreen goes straight to GLFW, which can target any monitor.

        private unsafe bool EnterExclusiveFullscreen(IMonitor monitor)
        {
            try
            {
                var native = Window.Native?.Glfw;
                if (native == null) return false;

                var glfw = Silk.NET.GLFW.Glfw.GetApi();
                var handle = (Silk.NET.GLFW.WindowHandle*)native.Value;

                var target = FindGlfwMonitor(glfw, monitor);
                if (target == null) return false;

                var videoMode = glfw.GetVideoMode(target);
                glfw.SetWindowMonitor(handle, target, 0, 0, videoMode->Width, videoMode->Height, videoMode->RefreshRate);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GRAPHICS] Could not enter exclusive fullscreen: {ex.Message}");
                return false;
            }
        }

        private unsafe void LeaveExclusiveFullscreen(Vector2D<int> position, Vector2D<int> size)
        {
            _exclusiveMonitorIndex = -1;

            try
            {
                var native = Window.Native?.Glfw;
                if (native == null) return;

                var glfw = Silk.NET.GLFW.Glfw.GetApi();
                var handle = (Silk.NET.GLFW.WindowHandle*)native.Value;

                glfw.SetWindowMonitor(handle, (Silk.NET.GLFW.Monitor*)null, position.X, position.Y, size.X, size.Y, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GRAPHICS] Could not leave exclusive fullscreen: {ex.Message}");
            }
        }

        /// <summary>Finds GLFW's handle for a Silk monitor by its desktop position (indices can differ).</summary>
        private static unsafe Silk.NET.GLFW.Monitor* FindGlfwMonitor(Silk.NET.GLFW.Glfw glfw, IMonitor monitor)
        {
            var monitors = glfw.GetMonitors(out int count);
            var origin = monitor.Bounds.Origin;

            for (int i = 0; i < count; i++)
            {
                glfw.GetMonitorPos(monitors[i], out int x, out int y);
                if (x == origin.X && y == origin.Y)
                    return monitors[i];
            }

            return monitor.Index >= 0 && monitor.Index < count ? monitors[monitor.Index] : null;
        }

        private (Vector2D<int> size, Vector2D<int> position) GetWindowedPlacement(IMonitor monitor)
        {
            var bounds = monitor.Bounds;

            var size = _hasWindowedPlacement ? _windowedSize : DefaultWindowedSize;
            size = new Vector2D<int>(
                Math.Min(Math.Max(size.X, 320), bounds.Size.X),
                Math.Min(Math.Max(size.Y, 200), bounds.Size.Y));

            // Back to the remembered spot, but only if its title bar is still on this monitor
            // (it might have been unplugged, or fullscreen may have moved us to another one).
            if (_hasWindowedPlacement)
            {
                int titleX = _windowedPosition.X + size.X / 2;
                int titleY = _windowedPosition.Y + 12;

                bool visible =
                    titleX >= bounds.Origin.X && titleX < bounds.Origin.X + bounds.Size.X &&
                    titleY >= bounds.Origin.Y && titleY < bounds.Origin.Y + bounds.Size.Y;

                if (visible) return (size, _windowedPosition);
            }

            return (size, new Vector2D<int>(
                bounds.Origin.X + (bounds.Size.X - size.X) / 2,
                bounds.Origin.Y + (bounds.Size.Y - size.Y) / 2));
        }

        private void AfterWindowChanged()
        {
            // The resize event also fires, but do it now so this frame already uses the new size.
            OnResize(Window.Size);
            //ParallaxController?.InvalidateWindowCache();
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
        public static string GetWindowMode() => Engine.Graphics.CurrentWindowMode.ToString();
        public static string GetResolution() => $"{Engine.Graphics.WindowSize.X}x{Engine.Graphics.WindowSize.Y}";
    }
}