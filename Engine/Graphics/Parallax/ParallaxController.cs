using System.Diagnostics;
using System.Numerics;
using OssianForge.Engine.Inputs;

namespace OssianForge.Engine.Graphics
{
    public sealed class ParallaxOptions
    {
        /// <summary>Physical size of the display's visible area in metres (width, height).
        /// 0.60 x 0.34 is a 27" 16:9 monitor; a 15.6" laptop is about 0.34 x 0.19.</summary>
        public Vector2 ScreenSizeMeters = new(0.60f, 0.34f);

        /// <summary>How many world units one real-world metre is. 1 = your world is in metres.
        /// Larger values make the screen look like a bigger window into the world.</summary>
        public float WorldUnitsPerMeter = 1f;

        /// <summary>When windowed, use the window's real size and position on the screen
        /// instead of assuming it fills the whole display.</summary>
        public bool FitToWindow = true;

        /// <summary>Where the eye is assumed to be when nobody is tracked (metres, screen-relative).</summary>
        public Vector3 RestPosition = new(0f, 0f, 0.6f);

        /// <summary>Closest and farthest head distances the camera will accept, in metres.</summary>
        public float MinDistance = 0.2f;
        public float MaxDistance = 2.5f;

        // One Euro filter, applied once per camera frame. Lower MinCutoff = steadier when still,
        // higher Beta = less lag when moving. Positions are in metres, so Beta is large.
        public float MinCutoff = 1.5f;
        public float Beta = 8f;
        public float DerivativeCutoff = 1.0f;

        /// <summary>Extra smoothing at render rate, hides the camera's 30 fps step.</summary>
        public float SmoothingSeconds = 0.04f;

        /// <summary>How slowly the eye eases back to RestPosition after tracking is lost.</summary>
        public float RestEaseSeconds = 0.6f;

        public HeadTrackerOptions Tracker = new();
    }

    /// <summary>
    /// Everything the camera needs for one frame of head-tracked parallax,
    /// in metres except WorldUnitsPerMeter. Eye is relative to the centre of the window:
    /// +X right, +Y up, +Z toward the viewer.
    /// </summary>
    public readonly struct ParallaxView
    {
        public readonly Vector3 Eye;
        public readonly Vector2 WindowSizeMeters;
        public readonly float WorldUnitsPerMeter;

        public ParallaxView(Vector3 eye, Vector2 windowSizeMeters, float worldUnitsPerMeter)
        {
            Eye = eye;
            WindowSizeMeters = windowSizeMeters;
            WorldUnitsPerMeter = worldUnitsPerMeter;
        }
    }

    /// <summary>
    /// Engine.Graphics.Parallax. Owns the head tracker, turns its raw detections into a steady
    /// eye position, works out the physical window the player is looking through, and gives the
    /// camera one small struct with all of it. The camera never talks to the tracker directly.
    /// (The class is not called Parallax because the namespace already is.)
    /// </summary>
    public sealed class ParallaxController
    {
        public readonly HeadTracker Tracker = new();
        public ParallaxOptions Options { get; private set; } = new();

        /// <summary>Master switch. When false the camera behaves exactly as before.</summary>
        public bool Enabled { get; set; }

        /// <summary>Print a [PARALLAX] status line once per second.</summary>
        public bool DebugLog = true;
        private double _logTimer;

        /// <summary>True while a person is being tracked (as opposed to the eye resting at RestPosition).</summary>
        public bool IsTracking => Tracker.IsPersonPresent;

        /// <summary>Current smoothed eye position relative to the screen centre, for debug overlays.</summary>
        public Vector3 EyeOffset => _eye;

        private ParallaxView _view;
        private bool _hasView;

        private Vector3 _eye;
        private Vector3 _filtered;
        private bool _eyeInitialized;
        private bool _wasPresent;
        private long _lastSequence;
        private long _lastSampleTicks;

        private readonly OneEuroFilter _filterX = new();
        private readonly OneEuroFilter _filterY = new();
        private readonly OneEuroFilter _filterZ = new();

        // Monitor size is cached because asking the OS every frame is wasteful.
        private double _monitorTimer = double.MaxValue;
        private int _monitorWidthPx;
        private int _monitorHeightPx;
        private int _monitorOriginX;
        private int _monitorOriginY;

        // ── Lifecycle ────────────────────────────────────────────────────────

        public bool Start(ParallaxOptions? options = null)
        {
            if (options != null) Options = options;
            _eyeInitialized = false;
            bool started = Tracker.Start(Options.Tracker);
            Console.WriteLine(
                $"[PARALLAX] Start -> trackerStarted={started} err={Tracker.LastError ?? "none"} " +
                $"configuredScreen={Options.ScreenSizeMeters.X:0.00}x{Options.ScreenSizeMeters.Y:0.00}m fitToWindow={Options.FitToWindow}");
            return started;
        }

        public void Stop()
        {
            Tracker.Stop();
            _hasView = false;
        }

        // ── Camera side ──────────────────────────────────────────────────────

        /// <summary>
        /// Gives the camera this frame's parallax data. Returns false when parallax is off,
        /// in which case the camera should use its normal symmetric projection.
        /// </summary>
        public bool TryGetView(out ParallaxView view)
        {
            view = _view;
            return Enabled && _hasView;
        }

        // ── Per-frame update (called from Graphics.UpdateRenderData) ─────────

        public void Update(double delta)
        {
            if (!Enabled)
            {
                _hasView = false;
                _eyeInitialized = false;
                return;
            }

            float dt = Math.Clamp((float)delta, 0.0001f, 0.1f);

            Vector3 target;
            float tau;

            if (Tracker.TryGetHead(out var head))
            {
                // The camera delivers ~30 samples a second but we render at 120, so only feed the
                // filter when a genuinely new sample has arrived, using the real time between them.
                if (head.FrameSequence != _lastSequence)
                {
                    float sampleDt = _lastSampleTicks == 0
                        ? 1f / 30f
                        : Math.Clamp((head.FrameTimestamp - _lastSampleTicks) / (float)Stopwatch.Frequency,
                                     0.005f, 0.25f);

                    _lastSequence = head.FrameSequence;
                    _lastSampleTicks = head.FrameTimestamp;

                    var o = Options;
                    _filtered = new Vector3(
                        _filterX.Filter(head.Position.X, sampleDt, o.MinCutoff, o.Beta, o.DerivativeCutoff),
                        _filterY.Filter(head.Position.Y, sampleDt, o.MinCutoff, o.Beta, o.DerivativeCutoff),
                        _filterZ.Filter(head.Position.Z, sampleDt, o.MinCutoff, o.Beta, o.DerivativeCutoff));
                }

                _wasPresent = true;
                target = _filtered;
                tau = Options.SmoothingSeconds;
            }
            else
            {
                if (_wasPresent)
                {
                    // Lost the person: forget filter history so the next one doesn't glide in from the old spot.
                    _filterX.Reset();
                    _filterY.Reset();
                    _filterZ.Reset();
                    _lastSampleTicks = 0;
                    _wasPresent = false;
                }

                target = Options.RestPosition;
                tau = Options.RestEaseSeconds;
            }

            target.Z = Math.Clamp(target.Z, Options.MinDistance, Options.MaxDistance);

            if (!_eyeInitialized)
            {
                _eye = target;
                _eyeInitialized = true;
            }
            else
            {
                float alpha = tau <= 0f ? 1f : 1f - MathF.Exp(-dt / tau);
                _eye = Vector3.Lerp(_eye, target, alpha);
            }

            ResolveWindow(delta, out Vector2 windowSize, out Vector2 windowCenterOffset);

            // The tracker reports the eye relative to the screen centre; the camera needs it
            // relative to the centre of the window, which differs when the window isn't fullscreen.
            Vector3 eyeInWindow = _eye - new Vector3(windowCenterOffset, 0f);

            _view = new ParallaxView(eyeInWindow, windowSize, Options.WorldUnitsPerMeter);
            _hasView = true;

            if (DebugLog)
            {
                _logTimer += delta;
                if (_logTimer >= 1.0)
                {
                    _logTimer = 0;
                    float verticalFov = float.RadiansToDegrees(2f * MathF.Atan(windowSize.Y * 0.5f / MathF.Max(eyeInWindow.Z, 0.05f)));
                    Console.WriteLine(
                        $"[PARALLAX] tracking={IsTracking} eye=({eyeInWindow.X:+0.00;-0.00},{eyeInWindow.Y:+0.00;-0.00},{eyeInWindow.Z:0.00})m " +
                        $"window={windowSize.X:0.00}x{windowSize.Y:0.00}m vfov={verticalFov:F0}deg " +
                        $"| tracker[thread={Tracker.IsRunning} latestDetected={Tracker.Latest.Detected} " +
                        $"lastFrameAge={Tracker.Latest.AgeMs:F0}ms err={Tracker.LastError ?? "none"}] " +
                        $"cam[open={CameraInput.Instance.IsOpen} fps={CameraInput.Instance.Fps:F0}]");
                }
            }
        }

        /// <summary>
        /// Physical size of the game window and the offset of its centre from the screen centre, in metres.
        /// </summary>
        private void ResolveWindow(double delta, out Vector2 sizeMeters, out Vector2 centerOffsetMeters)
        {
            sizeMeters = Options.ScreenSizeMeters;
            centerOffsetMeters = Vector2.Zero;

            if (!Options.FitToWindow) return;

            var window = Engine.Graphics.Window;
            if (window == null) return;

            _monitorTimer += delta;
            if (_monitorTimer >= 1.0 || _monitorWidthPx <= 0)
            {
                _monitorTimer = 0;
                var monitor = window.Monitor ?? Silk.NET.Windowing.Monitor.GetMainMonitor(window);
                var bounds = monitor.Bounds;
                _monitorWidthPx = bounds.Size.X;
                _monitorHeightPx = bounds.Size.Y;
                _monitorOriginX = bounds.Origin.X;
                _monitorOriginY = bounds.Origin.Y;
            }

            if (_monitorWidthPx <= 0 || _monitorHeightPx <= 0) return;

            float metersPerPixelX = Options.ScreenSizeMeters.X / _monitorWidthPx;
            float metersPerPixelY = Options.ScreenSizeMeters.Y / _monitorHeightPx;

            var size = window.Size;
            var position = window.Position;

            sizeMeters = new Vector2(size.X * metersPerPixelX, size.Y * metersPerPixelY);

            float windowCenterX = position.X + size.X * 0.5f - _monitorOriginX;
            float windowCenterY = position.Y + size.Y * 0.5f - _monitorOriginY;

            centerOffsetMeters = new Vector2(
                (windowCenterX - _monitorWidthPx * 0.5f) * metersPerPixelX,
                -(windowCenterY - _monitorHeightPx * 0.5f) * metersPerPixelY); // screen Y grows downward
        }

        // ── One Euro filter (Casiez et al.) ──────────────────────────────────

        private sealed class OneEuroFilter
        {
            private bool _initialized;
            private float _value;
            private float _derivative;

            public void Reset() => _initialized = false;

            public float Filter(float input, float dt, float minCutoff, float beta, float derivativeCutoff)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    _value = input;
                    _derivative = 0f;
                    return input;
                }

                float rawDerivative = (input - _value) / dt;
                _derivative += Alpha(dt, derivativeCutoff) * (rawDerivative - _derivative);

                // Still head: low cutoff, steady. Fast head: cutoff opens up, little lag.
                float cutoff = minCutoff + beta * MathF.Abs(_derivative);
                _value += Alpha(dt, cutoff) * (input - _value);
                return _value;
            }

            private static float Alpha(float dt, float cutoff)
            {
                float tau = 1f / (2f * MathF.PI * cutoff);
                return 1f / (1f + tau / dt);
            }
        }
    }
}