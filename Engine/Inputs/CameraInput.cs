using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace OssianForge.Engine.Inputs
{
    public enum CameraBackend { Auto, DirectShow, MediaFoundation, V4L2 }

    public enum CameraPixelFormat { Bgr24 }

    public sealed class CameraInputOptions
    {
        public int DeviceIndex = 0;
        public int Width = 640;
        public int Height = 480;
        public int Fps = 30;
        public CameraBackend Backend = CameraBackend.Auto;

        /// <summary>Ask for Motion-JPEG at 1280 wide and above. Uncompressed YUY2 tops out at 5-10 fps there on USB 2 cameras.</summary>
        public bool PreferMjpeg = true;

        /// <summary>
        /// Opening a webcam turns its privacy light on, so by default nothing opens
        /// until someone calls CameraInput.Start().
        /// </summary>
        public bool AutoStart = false;

        /// <summary>Delay before retrying when the device fails to open or disappears.</summary>
        public int RetryDelayMs = 2000;
    }

    /// <summary>
    /// One captured frame. Frames are pooled and reused, so never keep one past
    /// the lease that gave it to you; copy what you need.
    /// </summary>
    public sealed class CameraFrame
    {
        internal byte[] Data = Array.Empty<byte>();
        internal int Readers;

        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Stride => Width * 3;
        public CameraPixelFormat Format => CameraPixelFormat.Bgr24;

        /// <summary>Increases by one for every frame published. Starts at 1.</summary>
        public long Sequence { get; internal set; }

        /// <summary>Stopwatch ticks taken right after the frame was grabbed.</summary>
        public long Timestamp { get; internal set; }

        /// <summary>Tightly packed BGR, Width * Height * 3 bytes, top row first.</summary>
        public ReadOnlySpan<byte> Pixels => Data.AsSpan(0, Stride * Height);

        /// <summary>How old this frame is right now. Useful for latency compensation.</summary>
        public double AgeMs => (Stopwatch.GetTimestamp() - Timestamp) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Keeps a frame alive while you read it. Dispose exactly once (use a using block).
    /// </summary>
    public readonly struct CameraFrameLease : IDisposable
    {
        private readonly CameraInput? _owner;
        public CameraFrame Frame { get; }

        internal CameraFrameLease(CameraInput owner, CameraFrame frame)
        {
            _owner = owner;
            Frame = frame;
        }

        public void Dispose() => _owner?.Release(Frame);
    }

    /// <summary>
    /// Webcam input. Capture runs on a background thread at the camera's own rate;
    /// the engine's update loop never waits on hardware. Consumers ask for the
    /// newest frame by sequence number and read it in place through a lease.
    /// </summary>
    public sealed class CameraInput
    {
        private static Lazy<CameraInput> LazyInstance = new(() => new CameraInput());
        public static CameraInput Instance => LazyInstance.Value;

        private const int SlotCount = 4;

        private readonly object _lock = new();
        private readonly CameraFrame[] _slots = new CameraFrame[SlotCount];
        private CameraFrame? _latest;
        private long _sequence;
        private long _lastUpdateSequence;
        private long _droppedFrames;
        private long _readFailures;

        private CameraInputOptions _options = new();
        private Thread? _thread;
        private volatile bool _stop;
        private volatile bool _isOpen;
        private volatile string? _lastError;
        private int _width;
        private int _height;
        private double _fps;

        // ── State visible to the main thread ─────────────────────────────────
        public bool IsRunning => _thread is { IsAlive: true };
        public bool IsOpen => _isOpen;
        public int Width => Volatile.Read(ref _width);
        public int Height => Volatile.Read(ref _height);
        public double Fps => Volatile.Read(ref _fps);
        public long FrameSequence => Volatile.Read(ref _sequence);
        public long DroppedFrames => Interlocked.Read(ref _droppedFrames);
        public long ReadFailures => Interlocked.Read(ref _readFailures);
        public string? LastError => _lastError;
        public int DeviceIndex => _options.DeviceIndex;

        /// <summary>True for the one Update() in which a new frame has arrived (like IsKeyClicked).</summary>
        public bool HasNewFrame { get; private set; }

        private CameraInput()
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = new CameraFrame();
        }

        public void Initialize(CameraInputOptions options)
        {
            _options = options ?? new CameraInputOptions();
            if (_options.AutoStart) Start();
        }

        public void Update()
        {
            long seq = Volatile.Read(ref _sequence);
            HasNewFrame = seq != _lastUpdateSequence;
            _lastUpdateSequence = seq;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        /// <summary>Starts capturing. Returns immediately; watch IsOpen / LastError for the outcome.</summary>
        public void Start()
        {
            if (IsRunning) return;

            _stop = false;
            _lastError = null;
            _thread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "OssianForge.CameraInput"
            };
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            var thread = _thread;
            _thread = null;
            thread?.Join(3000);
            _isOpen = false;

            lock (_lock)
            {
                _latest = null;
                System.Threading.Monitor.PulseAll(_lock);   // wake anyone blocked in WaitForFrame so they can exit
            }
        }

        /// <summary>Call when the window closes so the capture thread and device are released.</summary>
        public void Shutdown() => Stop();

        /// <summary>Switch device or resolution; restarts capture if it was running.</summary>
        public void Reconfigure(CameraInputOptions options)
        {
            bool wasRunning = IsRunning;
            Stop();
            _options = options;
            if (wasRunning || options.AutoStart) Start();
        }

        // ── Reading frames ───────────────────────────────────────────────────

        /// <summary>
        /// Gets the newest frame if it is newer than afterSequence. Dispose the lease when done.
        /// Pass the Sequence of the last frame you handled (0 the first time).
        /// </summary>
        public bool TryAcquireLatest(long afterSequence, out CameraFrameLease lease)
        {
            lock (_lock)
            {
                if (_latest == null || _latest.Sequence <= afterSequence)
                {
                    lease = default;
                    return false;
                }

                _latest.Readers++;
                lease = new CameraFrameLease(this, _latest);
                return true;
            }
        }

        /// <summary>
        /// Blocks until a frame newer than afterSequence exists (true) or the timeout passes (false).
        /// Lets a consumer sleep on the camera's own clock instead of polling, which skips frames and adds latency.
        /// </summary>
        public bool WaitForFrame(long afterSequence, int timeoutMs)
        {
            long deadline = Stopwatch.GetTimestamp() + (long)(timeoutMs * (Stopwatch.Frequency / 1000.0));

            lock (_lock)
            {
                while (_latest == null || _latest.Sequence <= afterSequence)
                {
                    long remainingTicks = deadline - Stopwatch.GetTimestamp();
                    if (remainingTicks <= 0 || _stop) return false;

                    int waitMs = (int)Math.Max(1, remainingTicks * 1000 / Stopwatch.Frequency);
                    System.Threading.Monitor.Wait(_lock, waitMs);
                }

                return true;
            }
        }

        internal void Release(CameraFrame frame)
        {
            lock (_lock) { frame.Readers--; }
        }

        // ── Capture thread ───────────────────────────────────────────────────

        private void CaptureLoop()
        {
            var options = _options;

            // Debug overrides so you can try another camera/backend without recompiling:
            //   set OSSIAN_CAM_DEVICE=1   set OSSIAN_CAM_BACKEND=DirectShow  (or MediaFoundation / Auto)
            if (int.TryParse(Environment.GetEnvironmentVariable("OSSIAN_CAM_DEVICE"), out var envDevice))
                options.DeviceIndex = envDevice;
            if (Enum.TryParse<CameraBackend>(Environment.GetEnvironmentVariable("OSSIAN_CAM_BACKEND"), true, out var envBackend))
                options.Backend = envBackend;

            using var bgr = new Mat();

            while (!_stop)
            {
                try
                {
                    Console.WriteLine($"[INPUT] Opening camera device {options.DeviceIndex} via {options.Backend}...");
                    using var capture = new VideoCapture(options.DeviceIndex, MapBackend(options.Backend));

                    if (!capture.IsOpened())
                    {
                        Fail($"Could not open camera device {options.DeviceIndex}.");
                        SleepUnlessStopped(options.RetryDelayMs);
                        continue;
                    }

                    if (options.PreferMjpeg && options.Width >= 1280)
                        capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));

                    capture.Set(VideoCaptureProperties.FrameWidth, options.Width);
                    capture.Set(VideoCaptureProperties.FrameHeight, options.Height);
                    capture.Set(VideoCaptureProperties.Fps, options.Fps);

                    Volatile.Write(ref _width, (int)capture.Get(VideoCaptureProperties.FrameWidth));
                    Volatile.Write(ref _height, (int)capture.Get(VideoCaptureProperties.FrameHeight));

                    _isOpen = true;
                    _lastError = null;
                    int fourcc = (int)capture.Get(VideoCaptureProperties.FourCC);
                    string fcc = fourcc == 0 ? "?" : new string(new[]
                    {
                        (char)(fourcc & 255), (char)((fourcc >> 8) & 255),
                        (char)((fourcc >> 16) & 255), (char)((fourcc >> 24) & 255)
                    });
                    Console.WriteLine(
                        $"[INPUT] Camera {options.DeviceIndex} opened at {Width}x{Height} " +
                        $"(requested {options.Width}x{options.Height}@{options.Fps}) driverFps={capture.Get(VideoCaptureProperties.Fps):F0} fourcc={fcc}.");

                    ReadFrames(capture, bgr);
                }
                catch (Exception ex)
                {
                    Fail($"Camera capture error: {ex.Message}");
                }
                finally
                {
                    _isOpen = false;
                }

                if (!_stop) SleepUnlessStopped(options.RetryDelayMs);
            }
        }

        private void ReadFrames(VideoCapture capture, Mat bgr)
        {
            using var mat = new Mat();
            int consecutiveFailures = 0;
            bool loggedFirstFrame = false;
            int framesInWindow = 0;
            long windowStart = Stopwatch.GetTimestamp();

            while (!_stop)
            {
                if (!capture.Read(mat) || mat.Empty())
                {
                    // Device unplugged or stalled: give up after ~30 misses and reopen.
                    Interlocked.Increment(ref _readFailures);
                    if (++consecutiveFailures > 30)
                    {
                        Fail("Camera stopped delivering frames.");
                        return;
                    }
                    Thread.Sleep(5);
                    continue;
                }

                consecutiveFailures = 0;
                long timestamp = Stopwatch.GetTimestamp();

                if (!loggedFirstFrame)
                {
                    loggedFirstFrame = true;
                    var m = Cv2.Mean(mat);
                    // mean near (0,0,0) = black frame (shutter, privacy setting, IR camera). Equal B,G,R = grayscale/IR image.
                    Console.WriteLine(
                        $"[INPUT] first frame: {mat.Width}x{mat.Height} channels={mat.Channels()} type={mat.Type()} " +
                        $"meanBGR=({m.Val0:F0},{m.Val1:F0},{m.Val2:F0})");
                }

                Mat source = mat;
                if (mat.Channels() == 1)
                {
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
                    source = bgr;
                }
                else if (mat.Channels() == 4)
                {
                    Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);
                    source = bgr;
                }

                Publish(source, timestamp);

                framesInWindow++;
                long now = Stopwatch.GetTimestamp();
                double elapsed = (now - windowStart) / (double)Stopwatch.Frequency;
                if (elapsed >= 1.0)
                {
                    Volatile.Write(ref _fps, framesInWindow / elapsed);
                    framesInWindow = 0;
                    windowStart = now;
                }
            }
        }

        private void Publish(Mat frame, long timestamp)
        {
            if (_stop) return;

            int width = frame.Width;
            int height = frame.Height;
            int rowBytes = width * 3;
            int needed = rowBytes * height;

            // A slot is writable when it is not the newest frame and nobody is reading it.
            // Readers only ever take _latest, so a writable slot cannot gain a reader.
            CameraFrame? slot = null;
            lock (_lock)
            {
                foreach (var candidate in _slots)
                {
                    if (candidate != _latest && candidate.Readers == 0)
                    {
                        slot = candidate;
                        break;
                    }
                }
            }

            if (slot == null)
            {
                Interlocked.Increment(ref _droppedFrames);
                return;
            }

            if (slot.Data.Length < needed) slot.Data = new byte[needed];

            if (frame.IsContinuous())
            {
                Marshal.Copy(frame.Data, slot.Data, 0, needed);
            }
            else
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(frame.Ptr(y), slot.Data, y * rowBytes, rowBytes);
            }

            Volatile.Write(ref _width, width);
            Volatile.Write(ref _height, height);

            lock (_lock)
            {
                slot.Width = width;
                slot.Height = height;
                slot.Timestamp = timestamp;
                slot.Sequence = _sequence + 1;
                _latest = slot;
                Volatile.Write(ref _sequence, slot.Sequence);
                System.Threading.Monitor.PulseAll(_lock);   // wake the head tracker: a new frame is ready
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Debug: tries camera indices 0..maxIndex on DirectShow and Media Foundation and prints what each delivers.
        /// Only call while the camera is NOT running (the device is exclusive). HeadTracker calls this
        /// when the environment variable OSSIAN_CAM_PROBE=1 is set.
        /// </summary>
        public static void ProbeDevices(int maxIndex = 3)
        {
            foreach (var api in new[] { VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.MSMF })
            {
                for (int i = 0; i <= maxIndex; i++)
                {
                    try
                    {
                        using var capture = new VideoCapture(i, api);
                        if (!capture.IsOpened())
                        {
                            Console.WriteLine($"[INPUT] probe {api} #{i}: not available");
                            continue;
                        }

                        using var frame = new Mat();
                        bool ok = false;
                        for (int attempt = 0; attempt < 15 && !ok; attempt++)
                            ok = capture.Read(frame) && !frame.Empty();

                        if (!ok)
                        {
                            Console.WriteLine($"[INPUT] probe {api} #{i}: opened but delivered NO frames");
                            continue;
                        }

                        var m = Cv2.Mean(frame);
                        Console.WriteLine(
                            $"[INPUT] probe {api} #{i}: {frame.Width}x{frame.Height} channels={frame.Channels()} " +
                            $"meanBGR=({m.Val0:F0},{m.Val1:F0},{m.Val2:F0})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[INPUT] probe {api} #{i}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        private void Fail(string message)
        {
            if (_lastError != message) Console.WriteLine($"[INPUT] {message}");
            _lastError = message;
        }

        private void SleepUnlessStopped(int milliseconds)
        {
            for (int waited = 0; waited < milliseconds && !_stop; waited += 50)
                Thread.Sleep(50);
        }

        private static VideoCaptureAPIs MapBackend(CameraBackend backend) => backend switch
        {
            CameraBackend.DirectShow => VideoCaptureAPIs.DSHOW,
            CameraBackend.MediaFoundation => VideoCaptureAPIs.MSMF,
            CameraBackend.V4L2 => VideoCaptureAPIs.V4L2,
            _ => VideoCaptureAPIs.ANY
        };
    }
}