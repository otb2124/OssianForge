using System.Diagnostics;
using System.Numerics;
using OssianForge.Engine.Inputs;

namespace OssianForge.Engine.Graphics
{
    public enum ParallaxMode
    {
        /// <summary>
        /// Real-world geometry: the monitor is a window of its true physical size at its true distance,
        /// and one metre of head movement is one world unit. Correct for a diorama on your desk, but a
        /// scene several metres deep barely changes when you move 10 cm, so it mostly looks like a pan.
        /// </summary>
        Physical,

        /// <summary>
        /// Fish-tank / orbital: the screen becomes a window pinned to the camera's focus point (the zero
        /// parallax plane), and head movement is amplified into eye movement around that point. The focus
        /// stays put on screen while everything nearer and farther slides past it.
        /// Needs Camera.FocusDistance; cameras without one fall back to Physical.
        /// </summary>
        Focus
    }

    public sealed class ParallaxOptions
    {
        public ParallaxMode Mode = ParallaxMode.Focus;

        /// <summary>Focus mode: world units of eye movement per metre of sideways/vertical head movement.</summary>
        public float FocusGain = 6f;

        /// <summary>Focus mode: world units of eye movement per metre of head movement toward/away from the screen.
        /// Kept lower than FocusGain because depth changes feel stronger than sideways ones.</summary>
        public float FocusDepthGain = 3f;

        /// <summary>Focus mode: the eye never leaves the focus point by more than this fraction of the focus distance,
        /// sideways or up/down. It eases toward the limit instead of stopping dead.
        /// 0.35 is about 19 degrees around the focus point.</summary>
        public float FocusMaxOffsetFraction = 0.35f;

        /// <summary>Focus mode: the first tracked position becomes the neutral pose, so sitting off-centre
        /// doesn't tilt the view. Off = neutral is always RestPosition.</summary>
        public bool RecenterOnAcquire = true;

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
        // Sideways and vertical movement (X, Y):
        public float MinCutoff = 1.2f;
        public float Beta = 5f;
        public float DerivativeCutoff = 1.0f;

        // Depth (Z) is measured from the face box size, which is much noisier than the face position,
        // and one pixel of box width is several millimetres of depth. It gets its own, heavier filter.
        public float DepthMinCutoff = 0.5f;
        public float DepthBeta = 1.5f;

        /// <summary>Extra smoothing at render rate, hides the camera's 30 fps step.</summary>
        public float SmoothingSeconds = 0.04f;

        /// <summary>How slowly the eye eases to its rest pose when nobody has been seen yet.</summary>
        public float RestEaseSeconds = 0.6f;

        // ── When the face disappears ─────────────────────────────────────────
        // The eye does NOT snap back to neutral the moment the camera loses you. It stays where your head was,
        // and only drifts home if you stay gone.

        /// <summary>Face vanished in the middle of the image (turned away, covered, detector miss, too dark):
        /// the eye stays put this long.</summary>
        public float LostHoldSeconds = 1.2f;

        /// <summary>Face left through the edge of the camera image (you leaned out of view): the eye stays put this
        /// long, waiting for you to come back.</summary>
        public float EdgeHoldSeconds = 8f;

        /// <summary>...and keeps drifting this many metres further the same way, because the head really is beyond the edge.</summary>
        public float EdgeExtrapolationMeters = 0.08f;

        /// <summary>After holding gives up, how slowly the eye glides back to the neutral pose.</summary>
        public float ReturnEaseSeconds = 1.5f;

        /// <summary>When the face comes back after a short absence, how long the eye takes to glide to it instead of jumping.</summary>
        public float ReacquireEaseSeconds = 0.25f;

        /// <summary>Only an absence at least this long counts as a new person or a new seat, and recenters the neutral pose.
        /// Shorter ones (leaning out and back) keep the neutral pose, so the view carries on where it was.</summary>
        public float RecenterAfterAbsenceSeconds = 10f;

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

        // Focus mode data. HeadOffset is the head's displacement from its neutral pose in metres
        // (+X right, +Y up, +Z away from the screen).
        public readonly ParallaxMode Mode;
        public readonly Vector3 HeadOffset;
        public readonly float FocusGain;
        public readonly float FocusDepthGain;
        public readonly float FocusMaxOffsetFraction;

        public ParallaxView(
            Vector3 eye, Vector2 windowSizeMeters, float worldUnitsPerMeter,
            ParallaxMode mode = ParallaxMode.Physical, Vector3 headOffset = default,
            float focusGain = 0f, float focusDepthGain = 0f, float focusMaxOffsetFraction = 0.35f)
        {
            Eye = eye;
            WindowSizeMeters = windowSizeMeters;
            WorldUnitsPerMeter = worldUnitsPerMeter;
            Mode = mode;
            HeadOffset = headOffset;
            FocusGain = focusGain;
            FocusDepthGain = focusDepthGain;
            FocusMaxOffsetFraction = focusMaxOffsetFraction;
        }
    }

    public enum TrackState
    {
        /// <summary>Nobody has been seen yet.</summary>
        Idle,

        /// <summary>A face is being tracked.</summary>
        Tracking,

        /// <summary>The face left through an edge of the camera image; the eye waits for its return.</summary>
        HoldingEdge,

        /// <summary>The face vanished mid-image; the eye waits briefly in case it was a detector miss.</summary>
        HoldingLost,

        /// <summary>Holding gave up; the eye is gliding back to the neutral pose.</summary>
        Returning
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

        /// <summary>True while a face is actually being tracked (not while the eye is holding or returning).</summary>
        public bool IsTracking => _state == TrackState.Tracking;

        public TrackState State => _state;

        /// <summary>Current smoothed eye position relative to the screen centre, for debug overlays.</summary>
        public Vector3 EyeOffset => _eye;

        private ParallaxView _view;
        private bool _hasView;

        private Vector3 _eye;
        private Vector3 _neutral;   // head pose that means "looking straight at the focus"
        private Vector3 _filtered;
        private bool _eyeInitialized;
        private long _lastSequence;

        private TrackState _state = TrackState.Idle;
        private float _absenceSeconds;      // time since a face was last tracked
        private float _catchUpSeconds;      // remaining glide time after a short absence
        private bool _recenterPending;
        private Vector3 _lastTracked;       // last filtered pose while tracking
        private Vector3 _exitDirection;     // unit vector toward the image edge the face left through
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
            _neutral = Options.RestPosition;
            bool started = Tracker.Start(Options.Tracker);
            Console.WriteLine(
                $"[PARALLAX] Start -> trackerStarted={started} err={Tracker.LastError ?? "none"} " +
                $"configuredScreen={Options.ScreenSizeMeters.X:0.00}x{Options.ScreenSizeMeters.Y:0.00}m fitToWindow={Options.FitToWindow}");
            return started;
        }

        /// <summary>Makes the current head position the neutral pose (view straight at the focus). Bind this to a key.</summary>
        public void Recenter()
        {
            bool hasPose = _state is TrackState.Tracking or TrackState.HoldingEdge or TrackState.HoldingLost;
            _neutral = hasPose ? _eye : Options.RestPosition;
            Console.WriteLine($"[PARALLAX] Recentered at ({_neutral.X:+0.00;-0.00},{_neutral.Y:+0.00;-0.00},{_neutral.Z:0.00})m");
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
                _state = TrackState.Idle;
                return;
            }

            float dt = Math.Clamp((float)delta, 0.0001f, 0.1f);
            var o = Options;

            Vector3 target;
            float tau;

            if (Tracker.TryGetHead(out var head))
            {
                if (_state != TrackState.Tracking)
                {
                    // A face is (back) in view. A long gap means a new person or a new seat; a short one is the
                    // same person leaning out of view and back, and the view should carry on where it was.
                    bool newPerson = _state == TrackState.Idle || _absenceSeconds >= o.RecenterAfterAbsenceSeconds;

                    // After a long gap, or once the eye has already drifted home, the filters remember a pose
                    // that no longer applies. While merely holding they stay, so the motion stays continuous.
                    if (newPerson || _state == TrackState.Returning)
                        ResetFilters();

                    if (newPerson) _recenterPending = true;
                    else _catchUpSeconds = o.ReacquireEaseSeconds;

                    _state = TrackState.Tracking;
                }

                _absenceSeconds = 0f;

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

                    _filtered = new Vector3(
                        _filterX.Filter(head.Position.X, sampleDt, o.MinCutoff, o.Beta, o.DerivativeCutoff),
                        _filterY.Filter(head.Position.Y, sampleDt, o.MinCutoff, o.Beta, o.DerivativeCutoff),
                        _filterZ.Filter(head.Position.Z, sampleDt, o.DepthMinCutoff, o.DepthBeta, o.DerivativeCutoff));
                }

                if (_recenterPending && o.Mode == ParallaxMode.Focus && o.RecenterOnAcquire)
                {
                    // First sighting (or a new person): their pose becomes neutral, and the eye snaps there
                    // instead of gliding in from the resting pose, which would look like a swing.
                    _neutral = _filtered;
                    _eye = _filtered;
                    _eyeInitialized = true;
                }
                _recenterPending = false;

                _lastTracked = _filtered;
                target = _filtered;
                tau = _catchUpSeconds > 0f ? o.ReacquireEaseSeconds : o.SmoothingSeconds;
                _catchUpSeconds = MathF.Max(0f, _catchUpSeconds - dt);
            }
            else
            {
                _absenceSeconds += dt;

                if (_state == TrackState.Tracking)
                {
                    // The face just vanished. Where it was last seen says why.
                    var edges = FrameEdges.None;
                    if (Tracker.TryGetLastDetected(out var last)) edges = last.NearEdges;

                    _exitDirection = ExitDirection(edges);
                    _state = edges != FrameEdges.None ? TrackState.HoldingEdge : TrackState.HoldingLost;
                }

                if (_state == TrackState.HoldingEdge && _absenceSeconds > o.EdgeHoldSeconds) _state = TrackState.Returning;
                if (_state == TrackState.HoldingLost && _absenceSeconds > o.LostHoldSeconds) _state = TrackState.Returning;

                // In focus mode "home" is the neutral pose, so the offset eases back to exactly zero.
                Vector3 home = o.Mode == ParallaxMode.Focus ? _neutral : o.RestPosition;

                switch (_state)
                {
                    case TrackState.HoldingEdge:
                        {
                            // Stay at the last pose and creep a little further out: the head is beyond the edge.
                            float ramp = Math.Min(1f, _absenceSeconds / 0.4f);
                            target = _lastTracked + _exitDirection * (o.EdgeExtrapolationMeters * ramp);
                            tau = 0.25f;
                            break;
                        }

                    case TrackState.HoldingLost:
                        target = _lastTracked;
                        tau = o.SmoothingSeconds;
                        break;

                    case TrackState.Returning:
                        target = home;
                        tau = o.ReturnEaseSeconds;
                        break;

                    default: // Idle: nobody seen yet
                        target = home;
                        tau = o.RestEaseSeconds;
                        break;
                }
            }

            target.Z = Math.Clamp(target.Z, o.MinDistance, o.MaxDistance);

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

            _view = new ParallaxView(
                eyeInWindow, windowSize, o.WorldUnitsPerMeter,
                o.Mode, _eye - _neutral,
                o.FocusGain, o.FocusDepthGain, o.FocusMaxOffsetFraction);
            _hasView = true;

            if (DebugLog)
            {
                _logTimer += delta;
                if (_logTimer >= 1.0)
                {
                    _logTimer = 0;
                    float verticalFov = float.RadiansToDegrees(2f * MathF.Atan(windowSize.Y * 0.5f / MathF.Max(eyeInWindow.Z, 0.05f)));
                    Console.WriteLine(
                        $"[PARALLAX] state={_state} absent={_absenceSeconds:F1}s " +
                        $"eye=({Signed(eyeInWindow.X)},{Signed(eyeInWindow.Y)},{eyeInWindow.Z:0.00})m " +
                        $"mode={o.Mode} head=({Signed(_eye.X - _neutral.X)},{Signed(_eye.Y - _neutral.Y)},{Signed(_eye.Z - _neutral.Z)})m " +
                        $"window={windowSize.X:0.00}x{windowSize.Y:0.00}m vfov={verticalFov:F0}deg " +
                        $"| tracker[thread={Tracker.IsRunning} detected={Tracker.Latest.Detected} " +
                        $"frameAge={Tracker.Latest.AgeMs:F0}ms err={Tracker.LastError ?? "none"}] " +
                        $"cam[open={CameraInput.Instance.IsOpen} fps={CameraInput.Instance.Fps:F0}]");
                }
            }
        }

        private void ResetFilters()
        {
            _filterX.Reset();
            _filterY.Reset();
            _filterZ.Reset();
            _lastSampleTicks = 0;
        }

        /// <summary>
        /// Unit vector (screen space: +X right, +Y up) pointing toward the camera-image edge a face left through.
        /// The camera image is not mirrored, so with MirrorX the user's right is the image's left.
        /// </summary>
        private Vector3 ExitDirection(FrameEdges edges)
        {
            var t = Options.Tracker;
            float x = 0f, y = 0f;

            if (edges.HasFlag(FrameEdges.Left)) x += t.MirrorX ? 1f : -1f;
            if (edges.HasFlag(FrameEdges.Right)) x += t.MirrorX ? -1f : 1f;
            if (edges.HasFlag(FrameEdges.Top)) y += t.MirrorY ? -1f : 1f;
            if (edges.HasFlag(FrameEdges.Bottom)) y += t.MirrorY ? 1f : -1f;

            var v = new Vector3(x, y, 0f);
            return v == Vector3.Zero ? v : Vector3.Normalize(v);
        }

        // +0.05 / -0.05, and a clean +0.00 instead of "-+0.00" for tiny negative values.
        private static string Signed(float value) =>
            (MathF.Abs(value) < 0.005f ? 0f : value).ToString("+0.00;-0.00");

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