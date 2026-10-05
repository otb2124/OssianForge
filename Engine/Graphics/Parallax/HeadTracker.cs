using System.Diagnostics;
using System.Numerics;
using OssianForge.Engine.Inputs;

namespace OssianForge.Engine.Graphics
{
    public sealed class HeadTrackerOptions
    {
        /// <summary>Face model, relative to the executable's folder unless rooted.</summary>
        public string CascadePath = Path.Combine("Content", "ConfigFiles", "InputCamera", "haarcascade_frontalface.xml");

        /// <summary>Average width of the box the detector draws around a face, in metres.</summary>
        public float FaceWidthMeters = 0.15f;

        /// <summary>Horizontal field of view of the webcam. Most laptop cameras are 60 to 78 degrees.
        /// Use HeadTracker.CalibrateAtDistance() for a better value than this guess.</summary>
        public float HorizontalFovDegrees = 60f;

        /// <summary>Where the webcam sits relative to the centre of the screen, in metres
        /// (+X right, +Y up). A camera above the screen is roughly (0, 0.15).</summary>
        public Vector2 CameraOffsetMeters = Vector2.Zero;

        /// <summary>Webcam images are not mirrored, so the user's right is the image's left.
        /// Leave true so +X means "the user's right".</summary>
        public bool MirrorX = true;
        public bool MirrorY = false;

        /// <summary>Where the eye line sits inside the face box, 0 = top, 1 = bottom.</summary>
        public float EyeLineFraction = 0.4f;

        /// <summary>Frames are downscaled to this width before detection.</summary>
        public int DetectionWidth = 320;

        /// <summary>Smallest accepted face as a fraction of the frame width (limits max distance).</summary>
        public float MinFaceFraction = 0.12f;

        /// <summary>Cap on detections per second (0 = as fast as camera frames arrive).</summary>
        public int MaxDetectionsPerSecond = 30;

        /// <summary>A person counts as present until no face has been seen for this long.</summary>
        public int PresenceTimeoutMs = 400;

        /// <summary>Start the camera if nobody has, and stop it again when the tracker stops.</summary>
        public bool AutoStartCamera = true;

        /// <summary>Print a status line once per second.</summary>
        public bool DebugLog = true;
    }

    /// <summary>
    /// Result of analysing one camera frame. Immutable, safe to read from any thread.
    /// Position is in metres, relative to the centre of the screen:
    /// +X = the user's right, +Y = up, +Z = toward the viewer (so Z is the distance to the screen).
    /// </summary>
    public sealed class HeadSample
    {
        public static readonly HeadSample None = new();

        public bool Detected { get; init; }
        public Vector3 Position { get; init; }
        public float DistanceToScreen => Position.Z;

        /// <summary>Eye point inside the frame, 0..1 with the origin top-left.</summary>
        public Vector2 FrameCenter { get; init; }

        public float FacePixelWidth { get; init; }
        public float FrameWidth { get; init; }
        public float FrameHeight { get; init; }

        public long FrameSequence { get; init; }

        /// <summary>Stopwatch ticks when the camera grabbed the frame this sample came from.</summary>
        public long FrameTimestamp { get; init; }

        public double AgeMs => FrameTimestamp == 0
            ? double.PositiveInfinity
            : (Stopwatch.GetTimestamp() - FrameTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Watches CameraInput and reports whether a person is in front of the screen and how far away
    /// their head is. Detection runs on its own thread, so the render loop only ever reads the latest result.
    /// Distance comes from the face's apparent size (one camera, no depth sensor), so it is an estimate:
    /// call CalibrateAtDistance() once to tighten it.
    /// </summary>
    public sealed class HeadTracker
    {
        private HeadTrackerOptions _options = new();
        private IFaceDetector? _detector;
        private Thread? _thread;
        private volatile bool _stop;
        private bool _startedCamera;

        private HeadSample _latest = HeadSample.None;
        private HeadSample? _lastDetected;
        private float _focalOverWidth = 0.866f; // recomputed from the FOV on Start()
        private double _detectMs;
        private double _detectionsPerSecond;
        private HaarFaceDetector? _haar;
        private int _dbgFrames;   // frames analysed since the last status line
        private int _dbgFaces;    // of those, frames where a face was found

        public bool IsRunning => _thread is { IsAlive: true };
        public string? LastError { get; private set; }

        /// <summary>Most recent result, including frames where nobody was found.</summary>
        public HeadSample Latest => Volatile.Read(ref _latest);

        /// <summary>True while a face has been seen within PresenceTimeoutMs.</summary>
        public bool IsPersonPresent => TryGetHead(out _);

        /// <summary>Distance from the screen to the head in metres, or null if nobody is present.</summary>
        public float? DistanceToScreen => TryGetHead(out var head) ? head.DistanceToScreen : null;

        /// <summary>Smoothed time spent on one detection, for the debug overlay.</summary>
        public double DetectMs => Volatile.Read(ref _detectMs);
        public double DetectionsPerSecond => Volatile.Read(ref _detectionsPerSecond);

        /// <summary>Focal length as a fraction of the frame width. Save this after calibrating and set it on startup.</summary>
        public float FocalOverWidth
        {
            get => Volatile.Read(ref _focalOverWidth);
            set => Volatile.Write(ref _focalOverWidth, value);
        }

        /// <summary>The last head seen, if it was seen recently enough to count as present.</summary>
        public bool TryGetHead(out HeadSample head)
        {
            var sample = Volatile.Read(ref _lastDetected);
            if (sample != null && sample.AgeMs <= _options.PresenceTimeoutMs)
            {
                head = sample;
                return true;
            }

            head = HeadSample.None;
            return false;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        public bool Start(HeadTrackerOptions? options = null)
        {
            if (IsRunning) return true;
            if (options != null) _options = options;

            string path = Path.IsPathRooted(_options.CascadePath)
                ? _options.CascadePath
                : Path.Combine(AppContext.BaseDirectory, _options.CascadePath);

            Console.WriteLine($"[HEAD] cascade path: {path}");
            LogCascadeFile(path);

            try
            {
                _detector = new HaarFaceDetector(path, _options.DetectionWidth, _options.MinFaceFraction);
                _haar = _detector as HaarFaceDetector;
            }
            catch (Exception ex)
            {
                // Full exception on purpose: a missing OpenCV native DLL shows up here as
                // DllNotFoundException / TypeInitializationException, not as a cascade problem.
                Console.WriteLine($"[HEAD] detector creation failed:\n{ex}");
                return Fail($"Head tracker could not start: {ex.GetType().Name}: {ex.Message}");
            }

            FocalOverWidth = 0.5f / MathF.Tan(float.DegreesToRadians(_options.HorizontalFovDegrees) * 0.5f);
            LastError = null;

            var camera = CameraInput.Instance;
            _startedCamera = false;

            if (Environment.GetEnvironmentVariable("OSSIAN_CAM_PROBE") == "1" && !camera.IsRunning)
                CameraInput.ProbeDevices();
            if (_options.AutoStartCamera && !camera.IsRunning)
            {
                camera.Start();
                _startedCamera = true;
            }

            _stop = false;
            var detector = _detector;
            var activeOptions = _options;
            _thread = new Thread(() => Run(detector, activeOptions))
            {
                IsBackground = true,
                Name = "OssianForge.HeadTracker"
            };
            _thread.Start();

            Console.WriteLine(
                $"[HEAD] Head tracker started. cameraRunning={camera.IsRunning} startedCameraMyself={_startedCamera} " +
                $"fov={_options.HorizontalFovDegrees}deg faceWidth={_options.FaceWidthMeters}m detectWidth={_options.DetectionWidth} " +
                $"minFaceFraction={_options.MinFaceFraction} focal/width={FocalOverWidth:F3}");
            return true;
        }

        public void Stop()
        {
            _stop = true;
            var thread = _thread;
            _thread = null;

            bool finished = thread == null || thread.Join(3000);
            if (finished)
            {
                _detector?.Dispose();
                _detector = null;
            }

            if (_startedCamera)
            {
                CameraInput.Instance.Stop();
                _startedCamera = false;
            }

            Volatile.Write(ref _latest, HeadSample.None);
            Volatile.Write(ref _lastDetected, null);
        }

        // ── Calibration ──────────────────────────────────────────────────────

        /// <summary>
        /// Sit at a known distance from the screen (measure with a tape), look at the camera, and call this.
        /// It adjusts the focal length so the current face size maps to exactly that distance, which absorbs
        /// both the camera's real field of view and your real face width. Returns false if no face is visible.
        /// </summary>
        public bool CalibrateAtDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f || !TryGetHead(out var head) || head.FacePixelWidth <= 0f || head.FrameWidth <= 0f)
                return false;

            float focalPixels = distanceMeters * head.FacePixelWidth / _options.FaceWidthMeters;
            FocalOverWidth = focalPixels / head.FrameWidth;

            Console.WriteLine($"[HEAD] Calibrated at {distanceMeters:F2} m: focal/width = {FocalOverWidth:F3}");
            return true;
        }

        // ── Worker thread ────────────────────────────────────────────────────

        private void Run(IFaceDetector detector, HeadTrackerOptions options)
        {
            var camera = CameraInput.Instance;
            long lastSequence = 0;

            long minFrameTicks = options.MaxDetectionsPerSecond > 0
                ? Stopwatch.Frequency / options.MaxDetectionsPerSecond
                : 0;

            long logTimer = Stopwatch.GetTimestamp();
            long rateWindowStart = logTimer;
            int detectionsInWindow = 0;

            var haar = detector as HaarFaceDetector;
            int dumps = 0;
            long lastDumpTicks = 0;
            long lastFaceTicks = logTimer;
            bool hadFace = false;

            while (!_stop)
            {
                long now = Stopwatch.GetTimestamp();

                if (now - rateWindowStart >= Stopwatch.Frequency)
                {
                    double seconds = (now - rateWindowStart) / (double)Stopwatch.Frequency;
                    Volatile.Write(ref _detectionsPerSecond, detectionsInWindow / seconds);
                    detectionsInWindow = 0;
                    rateWindowStart = now;
                }

                if (options.DebugLog && now - logTimer >= Stopwatch.Frequency)
                {
                    logTimer = now;
                    LogStatus(camera);
                }

                if (!camera.TryAcquireLatest(lastSequence, out var lease))
                {
                    Thread.Sleep(2);
                    continue;
                }

                long started = Stopwatch.GetTimestamp();
                HeadSample sample;

                try
                {
                    using (lease)
                    {
                        var frame = lease.Frame;
                        lastSequence = frame.Sequence;

                        bool found = detector.TryDetect(frame.Pixels, frame.Width, frame.Height, out var face);

                        _dbgFrames++;
                        if (found)
                        {
                            _dbgFaces++;
                            lastFaceTicks = started;
                        }

                        if (options.DebugLog && found != hadFace)
                        {
                            hadFace = found;
                            Console.WriteLine(found
                                ? $"[HEAD] FACE ACQUIRED rect=({face.X:F0},{face.Y:F0}) {face.Width:F0}x{face.Height:F0}px in {frame.Width}x{frame.Height}"
                                : "[HEAD] FACE LOST");
                        }

                        // Save what the camera/detector actually see: the first frame, then every 5 s
                        // while no face has been found for 3 s (max 6 files). Open the PNGs and look.
                        if (options.DebugLog && haar != null && dumps < 6)
                        {
                            double noFaceSec = (started - lastFaceTicks) / (double)Stopwatch.Frequency;
                            double sinceDump = lastDumpTicks == 0
                                ? double.MaxValue
                                : (started - lastDumpTicks) / (double)Stopwatch.Frequency;

                            if (dumps == 0 || (!found && noFaceSec >= 3.0 && sinceDump >= 5.0))
                            {
                                string baseName = Path.Combine(AppContext.BaseDirectory, "headtracker-debug", $"frame{dumps:D2}");
                                bool saved = haar.SaveDebugFrame(baseName);
                                Console.WriteLine($"[HEAD] debug frame saved={saved}: {baseName}_camera.png / _detector_input.png (face={found})");
                                dumps++;
                                lastDumpTicks = started;
                            }
                        }

                        sample = found
                            ? BuildSample(face, frame.Width, frame.Height, frame.Sequence, frame.Timestamp, options)
                            : new HeadSample
                            {
                                Detected = false,
                                FrameWidth = frame.Width,
                                FrameHeight = frame.Height,
                                FrameSequence = frame.Sequence,
                                FrameTimestamp = frame.Timestamp
                            };
                    }
                }
                catch (Exception ex)
                {
                    LastError = $"Head detection error: {ex.Message}";
                    Console.WriteLine($"[HEAD] {LastError}");
                    Thread.Sleep(100);
                    continue;
                }

                Volatile.Write(ref _latest, sample);
                if (sample.Detected) Volatile.Write(ref _lastDetected, sample);

                long finished = Stopwatch.GetTimestamp();
                double ms = (finished - started) * 1000.0 / Stopwatch.Frequency;
                double previous = Volatile.Read(ref _detectMs);
                Volatile.Write(ref _detectMs, previous == 0 ? ms : previous * 0.9 + ms * 0.1);
                detectionsInWindow++;

                // Don't spin faster than the configured rate; the camera is the limit anyway.
                long elapsed = finished - started;
                if (elapsed < minFrameTicks)
                    Thread.Sleep((int)((minFrameTicks - elapsed) * 1000 / Stopwatch.Frequency));
            }
        }

        private HeadSample BuildSample(FaceRect face, int width, int height, long sequence, long timestamp,
                                       HeadTrackerOptions options)
        {
            float focalPx = FocalOverWidth * width;

            // Pinhole model: the smaller the face looks, the farther away it is.
            float z = focalPx * options.FaceWidthMeters / MathF.Max(face.Width, 1f);

            float eyeX = face.X + face.Width * 0.5f;
            float eyeY = face.Y + face.Height * options.EyeLineFraction;

            float x = (eyeX - width * 0.5f) * z / focalPx;
            float y = -(eyeY - height * 0.5f) * z / focalPx; // image Y grows downward
            if (options.MirrorX) x = -x;
            if (options.MirrorY) y = -y;

            return new HeadSample
            {
                Detected = true,
                Position = new Vector3(x + options.CameraOffsetMeters.X, y + options.CameraOffsetMeters.Y, z),
                FrameCenter = new Vector2(eyeX / width, eyeY / height),
                FacePixelWidth = face.Width,
                FrameWidth = width,
                FrameHeight = height,
                FrameSequence = sequence,
                FrameTimestamp = timestamp
            };
        }

        private static void LogCascadeFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    Console.WriteLine("[HEAD] cascade file DOES NOT EXIST at that path (check CopyToOutputDirectory / file name).");
                    return;
                }

                string text = File.ReadAllText(path);
                string head = text.Substring(0, Math.Min(80, text.Length)).Replace('\r', ' ').Replace('\n', ' ');
                bool hasStorage = text.Contains("<opencv_storage>");
                bool hasCascade = text.Contains("<cascade") || text.Contains("<stages>");
                bool looksHtml = text.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
                                 || text.Contains("<html");

                Console.WriteLine(
                    $"[HEAD] cascade file: {info.Length} bytes, opencv_storage={hasStorage}, cascadeNodes={hasCascade}, " +
                    $"looksLikeHtml={looksHtml}, starts=\"{head}\"");

                if (info.Length < 100_000 || !hasStorage || !hasCascade || looksHtml)
                    Console.WriteLine("[HEAD] WARNING: this does not look like a real Haar cascade (real ones are several hundred KB). " +
                                      "Re-download haarcascade_frontalface_default.xml from the opencv/data folder as a RAW file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HEAD] could not inspect cascade file: {ex.Message}");
            }
        }

        private void LogStatus(CameraInput camera)
        {
            string timing = $"detect={DetectMs:F1}ms rate={DetectionsPerSecond:F1}/s";

            string cam =
                $"cam[open={camera.IsOpen} running={camera.IsRunning} {camera.Width}x{camera.Height} fps={camera.Fps:F1} " +
                $"seq={camera.FrameSequence} dropped={camera.DroppedFrames} readFail={camera.ReadFailures} err={camera.LastError ?? "none"}]";

            string det = $"det[analyzed={_dbgFrames} withFace={_dbgFaces}";
            if (_haar != null)
                det += $" strict={_haar.LastRawCount} loose={_haar.LastRelaxedCount} luma={_haar.LastMeanLuma:F0} input={_haar.LastInputWidth}px";
            det += "]";
            _dbgFrames = 0;
            _dbgFaces = 0;

            if (!camera.IsOpen)
            {
                Console.WriteLine($"[HEAD] waiting for camera ({camera.LastError ?? "starting..."}) {cam}");
            }
            else if (TryGetHead(out var head))
            {
                var p = head.Position;
                Console.WriteLine(
                    $"[HEAD] present=True distance={p.Z:F2}m pos=({p.X:+0.00;-0.00},{p.Y:+0.00;-0.00},{p.Z:0.00}) " +
                    $"face={head.FacePixelWidth:F0}px {timing} {cam} {det}");
            }
            else
            {
                Console.WriteLine($"[HEAD] present=False (no face in frame) {timing} {cam} {det}");
            }
        }

        private bool Fail(string message)
        {
            LastError = message;
            Console.WriteLine($"[HEAD] {message}");
            return false;
        }
    }
}