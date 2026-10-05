using OpenCvSharp;
using OssianForge.Engine.Inputs;

namespace OssianForge.Engine.Devices.Providers
{
    /// <summary>
    /// OpenCV cannot list cameras, so this opens indices 0..N-1 one after another. Opening lights the
    /// camera's privacy indicator, which is why Enumerate() only returns the cached result of the last
    /// Probe() and the poll timer never causes a probe. Probing is skipped while CameraInput holds a
    /// camera, because the device is exclusive.
    /// </summary>
    public sealed class CameraProvider : IDeviceProvider
    {
        private readonly List<DeviceInfo> _cache = new();

        public string Name => "OpenCV camera probe";
        public int ProbeCount = 4;
        public CameraBackend Backend = CameraBackend.Auto;

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
                    using var capture = new VideoCapture(i, api);
                    if (!capture.IsOpened()) continue;

                    int width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                    int height = (int)capture.Get(VideoCaptureProperties.FrameHeight);

                    found.Add(new DeviceInfo(
                        DeviceKind.Camera, $"camera:{i}", $"Camera #{i}", i, found.Count == 0, $"{width}x{height}"));
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

        private static VideoCaptureAPIs MapBackend(CameraBackend backend) => backend switch
        {
            CameraBackend.DirectShow => VideoCaptureAPIs.DSHOW,
            CameraBackend.MediaFoundation => VideoCaptureAPIs.MSMF,
            CameraBackend.V4L2 => VideoCaptureAPIs.V4L2,
            _ => VideoCaptureAPIs.ANY
        };
    }
}