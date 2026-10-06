using OpenCvSharp;
using OssianForge.Engine.Inputs;

namespace OssianForge.Engine.Devices.Providers
{
    /// <summary>
    /// OpenCV cannot list cameras, so this opens indices 0..N-1 one after another. Opening lights the
    /// camera's privacy indicator, which is why Enumerate() only returns the cached result of the last
    /// Probe() and the poll timer never causes a probe. Probing is skipped while CameraInput holds a
    /// camera, because the device is exclusive.
    ///
    /// Mode probing opens a FRESH capture per candidate resolution and sets the size before the first
    /// read. Changing the size of a Media Foundation capture that is already streaming makes it fail with
    /// "initStream Failed to select stream 0" and a "_step >= minstep" exception from Read(), and it stays
    /// broken for every later size on that capture object.
    /// </summary>
    public sealed class CameraProvider : IDeviceProvider
    {
        private readonly List<DeviceInfo> _cache = new();

        public string Name => "OpenCV camera probe";
        public int ProbeCount = 4;
        public CameraBackend Backend = CameraBackend.Auto;

        /// <summary>
        /// Also find out which resolutions each camera really delivers, by asking for a few common ones and
        /// reading a frame at each. Adds a little time per camera at startup, but lets the engine pick a
        /// resolution that suits the camera instead of assuming 640x480.
        /// </summary>
        public bool ProbeModes = true;

        private static readonly (int Width, int Height)[] CandidateModes =
        {
            (320, 240), (640, 480), (800, 600), (1280, 720), (1920, 1080)
        };

        static CameraProvider()
        {
            // Only effective if OpenCV's native library has not been loaded yet (it reads these once).
            // Quiets the "[ WARN ] cap_msmf.cpp ... Failed to select stream 0" spam from rejected sizes, and
            // skips MSMF's slow hardware-transform setup, which makes every open take much longer.
            SetEnvIfUnset("OPENCV_LOG_LEVEL", "ERROR");
            SetEnvIfUnset("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0");
        }

        private static void SetEnvIfUnset(string name, string value)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value);
        }

        public void Enumerate(List<DeviceInfo> into) => into.AddRange(_cache);

        /// <summary>Returns false when the probe was skipped and the previous result was kept.</summary>
        public bool Probe()
        {
            if (CameraInput.Instance.IsRunning)
            {
                Console.WriteLine("[DEVICES] Camera probe skipped: CameraInput is running and owns its device.");
                return false;
            }

            var found = new List<DeviceInfo>();
            var api = MapBackend(Backend);

            for (int i = 0; i < ProbeCount; i++)
            {
                try
                {
                    int width, height;
                    using (var capture = new VideoCapture(i, api))
                    {
                        if (!capture.IsOpened()) continue;
                        width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                        height = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                    }   // released before the mode probe opens its own captures

                    // The default mode comes first, then whatever the camera really delivers.
                    var modes = new List<CameraMode>();
                    if (width > 0 && height > 0) modes.Add(new CameraMode(width, height));
                    if (ProbeModes) AddDeliveredModes(i, api, modes);
                    modes.Sort((a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));

                    string detail = $"{width}x{height}" + (modes.Count > 1 ? $" (delivers {string.Join(", ", modes)})" : "");

                    found.Add(new DeviceInfo(
                        DeviceKind.Camera, $"camera:{i}", $"Camera #{i}", i, found.Count == 0, detail, modes));
                }
                catch (Exception ex)
                {
                    // A missing OpenCV native library throws here; stop instead of failing four times.
                    Console.WriteLine($"[DEVICES] Camera probe failed: {ex.GetType().Name}: {ex.Message}");
                    break;
                }
            }

            _cache.Clear();
            _cache.AddRange(found);
            return true;
        }

        /// <summary>
        /// Drivers often report the size you asked for even when they can't deliver it, so each candidate is
        /// checked by reading a real frame and taking its size. Every candidate gets its own capture, with the
        /// size set before the first read, because a streaming capture can't reliably change size.
        /// A size the camera rejects is simply "not delivered"; it is not an error.
        /// </summary>
        private static void AddDeliveredModes(int deviceIndex, VideoCaptureAPIs api, List<CameraMode> modes)
        {
            var rejected = new List<string>();

            foreach (var (width, height) in CandidateModes)
            {
                if (TryReadMode(deviceIndex, api, width, height, out var delivered))
                {
                    if (!modes.Contains(delivered)) modes.Add(delivered);
                }
                else
                {
                    rejected.Add($"{width}x{height}");
                }
            }

            if (rejected.Count > 0)
                Console.WriteLine($"[DEVICES] Camera #{deviceIndex}: not delivered: {string.Join(", ", rejected)}");
        }

        private static bool TryReadMode(int deviceIndex, VideoCaptureAPIs api, int width, int height, out CameraMode mode)
        {
            mode = default;
            try
            {
                using var capture = new VideoCapture(deviceIndex, api);
                if (!capture.IsOpened()) return false;

                // Size first, before any frame has been read.
                capture.Set(VideoCaptureProperties.FrameWidth, width);
                capture.Set(VideoCaptureProperties.FrameHeight, height);

                using var frame = new Mat();
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    if (capture.Read(frame) && !frame.Empty())
                    {
                        mode = new CameraMode(frame.Width, frame.Height);
                        return true;
                    }
                }
                return false;
            }
            catch (OpenCVException)
            {
                // e.g. "_step >= minstep": the driver handed back a buffer that doesn't match the size.
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEVICES] Camera mode {width}x{height} probe failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
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