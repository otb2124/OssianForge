using OpenCvSharp;

namespace OssianForge.Engine.Graphics
{
    /// <summary>A detected face in full-resolution frame pixels (origin top-left).</summary>
    public readonly record struct FaceRect(float X, float Y, float Width, float Height);

    /// <summary>
    /// Finds the biggest (closest) face in a BGR frame. Implementations are used from a
    /// single worker thread and need not be thread-safe. Swap this out for a better
    /// detector (landmarks, DNN) without touching HeadTracker.
    /// </summary>
    public interface IFaceDetector : IDisposable
    {
        bool TryDetect(ReadOnlySpan<byte> bgr, int width, int height, out FaceRect face);
    }

    /// <summary>
    /// OpenCV Haar cascade detector. Cheap and dependency-free, but frontal faces only
    /// and a little jittery, which is fine for presence and distance.
    /// </summary>
    public sealed unsafe class HaarFaceDetector : IFaceDetector
    {
        private readonly CascadeClassifier _cascade;
        private readonly int _detectWidth;
        private readonly float _minFaceFraction;

        private Mat? _bgr;
        private Mat? _gray;
        private Mat? _small;
        private Mat? _equalized;
        private int _allocatedWidth;
        private int _allocatedHeight;

        // ── Diagnostics (read by HeadTracker's debug log) ──
        public bool Diagnostics = true;
        public int LastRawCount { get; private set; }      // faces found with the normal settings
        public int LastRelaxedCount { get; private set; }  // faces found with loose settings (only computed when the strict pass finds none)
        public double LastMeanLuma { get; private set; }   // 0 = black frame, 255 = white
        public int LastInputWidth { get; private set; }    // width of the image the cascade actually saw

        /// <param name="cascadePath">Path to haarcascade_frontalface_default.xml.</param>
        /// <param name="detectWidth">Frames are downscaled to this width before detection (speed).</param>
        /// <param name="minFaceFraction">Smallest face to accept, as a fraction of the frame width.</param>
        public HaarFaceDetector(string cascadePath, int detectWidth = 320, float minFaceFraction = 0.12f)
        {
            if (!File.Exists(cascadePath))
                throw new FileNotFoundException($"Face cascade not found: {cascadePath}", cascadePath);

            _cascade = new CascadeClassifier(cascadePath);
            if (_cascade.Empty())
                throw new InvalidOperationException($"Face cascade could not be loaded: {cascadePath}");

            _detectWidth = Math.Max(64, detectWidth);
            _minFaceFraction = minFaceFraction;
        }

        public bool TryDetect(ReadOnlySpan<byte> bgr, int width, int height, out FaceRect face)
        {
            face = default;
            if (width <= 0 || height <= 0 || bgr.Length < width * height * 3) return false;

            EnsureMats(width, height);

            // Copy the leased frame into our own Mat so the camera can recycle its buffer.
            var destination = new Span<byte>(_bgr!.Data.ToPointer(), width * height * 3);
            bgr.Slice(0, destination.Length).CopyTo(destination);

            Cv2.CvtColor(_bgr, _gray!, ColorConversionCodes.BGR2GRAY);

            Mat input = _gray!;
            float scale = 1f;
            if (width > _detectWidth)
            {
                scale = width / (float)_detectWidth;
                int smallHeight = Math.Max(1, (int)MathF.Round(height / scale));
                Cv2.Resize(_gray!, _small!, new Size(_detectWidth, smallHeight), 0, 0, InterpolationFlags.Area);
                input = _small!;
            }

            Cv2.EqualizeHist(input, _equalized!);

            LastInputWidth = input.Width;
            if (Diagnostics) LastMeanLuma = Cv2.Mean(_gray!).Val0;

            int minFace = Math.Max(24, (int)(input.Width * _minFaceFraction));
            Rect[] faces = _cascade.DetectMultiScale(
                _equalized!, 1.1, 4, HaarDetectionTypes.ScaleImage, new Size(minFace, minFace));

            LastRawCount = faces.Length;
            if (faces.Length == 0)
            {
                // If this loose pass finds something, the strict settings (minNeighbors / MinFaceFraction)
                // are the problem. If it never does, look at the camera image or the cascade file instead.
                if (Diagnostics)
                {
                    Rect[] loose = _cascade.DetectMultiScale(
                        _equalized!, 1.1, 2, HaarDetectionTypes.ScaleImage, new Size(24, 24));
                    LastRelaxedCount = loose.Length;
                }
                return false;
            }
            LastRelaxedCount = faces.Length;

            // Several people: track the largest face, i.e. the closest person.
            Rect best = faces[0];
            for (int i = 1; i < faces.Length; i++)
                if (faces[i].Width > best.Width) best = faces[i];

            face = new FaceRect(best.X * scale, best.Y * scale, best.Width * scale, best.Height * scale);
            return true;
        }

        /// <summary>Writes the last frame (as the camera delivered it) and the image the cascade saw. Call from the detection thread.</summary>
        public bool SaveDebugFrame(string pathWithoutExtension)
        {
            if (_bgr == null || _equalized == null) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(pathWithoutExtension)!);
            bool a = Cv2.ImWrite(pathWithoutExtension + "_camera.png", _bgr);
            bool b = Cv2.ImWrite(pathWithoutExtension + "_detector_input.png", _equalized);
            return a && b;
        }

        private void EnsureMats(int width, int height)
        {
            if (_bgr != null && _allocatedWidth == width && _allocatedHeight == height) return;

            _bgr?.Dispose();
            _gray?.Dispose();
            _small?.Dispose();
            _equalized?.Dispose();

            _bgr = new Mat(height, width, MatType.CV_8UC3);
            _gray = new Mat();
            _small = new Mat();
            _equalized = new Mat();

            _allocatedWidth = width;
            _allocatedHeight = height;
        }

        public void Dispose()
        {
            _cascade.Dispose();
            _bgr?.Dispose();
            _gray?.Dispose();
            _small?.Dispose();
            _equalized?.Dispose();
        }
    }
}