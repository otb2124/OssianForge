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
    /// OpenCV Haar cascade detector (frontal faces only).
    ///
    /// Two modes keep it fast and steady:
    ///  - Full search: the whole frame, downscaled. Only runs to find a face, and less often while nobody is there.
    ///  - Tracking: once a face is known, only a window around its last position is searched, at full camera
    ///    resolution, and only for faces of a similar size. That is several times cheaper, gives a more precise
    ///    box than a downscaled full frame (the box size is what depth is computed from), and cannot jump to
    ///    a false detection elsewhere in the room.
    /// </summary>
    public sealed unsafe class HaarFaceDetector : IFaceDetector
    {
        private const int MaxTrackMisses = 15;   // frames the tracking window keeps looking before a full search takes over alone
        private const int MaxRoiWidth = 360;     // a tracking window wider than this is downscaled before detection

        private readonly CascadeClassifier _cascade;
        private readonly int _searchWidth;

        private Mat? _bgr;
        private Mat? _gray;
        private Mat? _small;
        private Mat? _equalized;
        private Mat? _roiGray;
        private Mat? _roiSmall;
        private Mat? _roiEqualized;
        private int _allocatedWidth;
        private int _allocatedHeight;

        private bool _hasTrack;
        private FaceRect _track;
        private int _trackMisses;
        private int _framesWithoutFace;
        private long _frameCounter;

        /// <summary>Smallest face to accept, in full-resolution pixels. HeadTracker sets this from the camera and the maximum tracking distance.</summary>
        public float MinFacePixels = 24f;

        // ── Diagnostics (read by HeadTracker's debug log) ──
        public bool Diagnostics = true;
        public bool IsTracking => _hasTrack;
        public int RoiHits { get; private set; }          // frames answered by the cheap tracking window
        public int FullSearches { get; private set; }     // frames that needed a whole-frame search
        public double LastMeanLuma { get; private set; }  // 0 = black frame, 255 = white. Dim light is the usual cause of lost faces.
        public int LastInputWidth { get; private set; }   // width of the image the cascade last saw

        /// <param name="cascadePath">Path to the frontal face cascade XML.</param>
        /// <param name="searchWidth">Whole-frame searches are downscaled to this width (speed).</param>
        public HaarFaceDetector(string cascadePath, int searchWidth = 480)
        {
            if (!File.Exists(cascadePath))
                throw new FileNotFoundException($"Face cascade not found: {cascadePath}", cascadePath);

            _cascade = new CascadeClassifier(cascadePath);
            if (_cascade.Empty())
                throw new InvalidOperationException($"Face cascade could not be loaded: {cascadePath}");

            _searchWidth = Math.Max(64, searchWidth);
        }

        /// <summary>Forget the tracked face so the next call searches the whole frame.</summary>
        public void ResetTracking()
        {
            _hasTrack = false;
            _trackMisses = 0;
        }

        /// <summary>Clears the RoiHits / FullSearches counters (the debug log does this once per second).</summary>
        public void ResetCounters()
        {
            RoiHits = 0;
            FullSearches = 0;
        }

        public bool TryDetect(ReadOnlySpan<byte> bgr, int width, int height, out FaceRect face)
        {
            face = default;
            if (width <= 0 || height <= 0 || bgr.Length < width * height * 3) return false;

            EnsureMats(width, height);
            _frameCounter++;

            // Copy the leased frame into our own Mat so the camera can recycle its buffer.
            var destination = new Span<byte>(_bgr!.Data.ToPointer(), width * height * 3);
            bgr.Slice(0, destination.Length).CopyTo(destination);

            if (Diagnostics && _frameCounter % 30 == 1)
            {
                Cv2.CvtColor(_bgr, _gray!, ColorConversionCodes.BGR2GRAY);
                LastMeanLuma = Cv2.Mean(_gray!).Val0;
            }

            if (_hasTrack)
            {
                if (TrackInWindow(width, height, out face))
                {
                    RoiHits++;
                    _track = face;
                    _trackMisses = 0;
                    _framesWithoutFace = 0;
                    return true;
                }

                if (++_trackMisses > MaxTrackMisses) _hasTrack = false;
            }

            // Nobody around for a while: the whole-frame search is the expensive path, so do it every third frame.
            _framesWithoutFace++;
            if (!_hasTrack && _framesWithoutFace > 30 && _frameCounter % 3 != 0)
                return false;

            if (SearchWholeFrame(width, height, out face))
            {
                _hasTrack = true;
                _track = face;
                _trackMisses = 0;
                _framesWithoutFace = 0;
                return true;
            }

            return false;
        }

        private bool TrackInWindow(int width, int height, out FaceRect face)
        {
            face = default;

            // Window around the last face, 0.6 face widths of room on every side.
            float pad = _track.Width * 0.6f;
            int x0 = Math.Max(0, (int)(_track.X - pad));
            int y0 = Math.Max(0, (int)(_track.Y - pad));
            int x1 = Math.Min(width, (int)(_track.X + _track.Width + pad));
            int y1 = Math.Min(height, (int)(_track.Y + _track.Height + pad));
            int roiWidth = x1 - x0;
            int roiHeight = y1 - y0;
            if (roiWidth < 48 || roiHeight < 48) return false;

            using var crop = new Mat(_bgr!, new Rect(x0, y0, roiWidth, roiHeight));
            Cv2.CvtColor(crop, _roiGray!, ColorConversionCodes.BGR2GRAY);

            Mat input = _roiGray!;
            float k = 1f;
            if (roiWidth > MaxRoiWidth)
            {
                k = MaxRoiWidth / (float)roiWidth;
                Cv2.Resize(_roiGray!, _roiSmall!, new Size(MaxRoiWidth, Math.Max(1, (int)MathF.Round(roiHeight * k))),
                           0, 0, InterpolationFlags.Area);
                input = _roiSmall!;
            }

            Cv2.EqualizeHist(input, _roiEqualized!);
            LastInputWidth = input.Width;

            // Only look for faces about as big as the last one: far fewer scales to test, and no false hits.
            int minSize = Math.Max(24, (int)(_track.Width * 0.7f * k));
            int maxSize = Math.Min((int)(_track.Width * 1.45f * k), Math.Min(input.Width, input.Height));
            if (maxSize <= minSize) return false;

            Rect[] faces = _cascade.DetectMultiScale(
                _roiEqualized!, 1.08, 3,
                HaarDetectionTypes.FindBiggestObject | HaarDetectionTypes.ScaleImage,
                new Size(minSize, minSize), new Size(maxSize, maxSize));

            if (faces.Length == 0) return false;

            Rect best = Biggest(faces);
            face = new FaceRect(x0 + best.X / k, y0 + best.Y / k, best.Width / k, best.Height / k);
            return true;
        }

        private bool SearchWholeFrame(int width, int height, out FaceRect face)
        {
            face = default;
            FullSearches++;

            Cv2.CvtColor(_bgr!, _gray!, ColorConversionCodes.BGR2GRAY);
            if (Diagnostics) LastMeanLuma = Cv2.Mean(_gray!).Val0;

            Mat input = _gray!;
            float k = 1f;
            if (width > _searchWidth)
            {
                k = _searchWidth / (float)width;
                Cv2.Resize(_gray!, _small!, new Size(_searchWidth, Math.Max(1, (int)MathF.Round(height * k))),
                           0, 0, InterpolationFlags.Area);
                input = _small!;
            }

            Cv2.EqualizeHist(input, _equalized!);
            LastInputWidth = input.Width;

            int minFace = Math.Max(24, (int)(MinFacePixels * k));
            Rect[] faces = _cascade.DetectMultiScale(
                _equalized!, 1.1, 4,
                HaarDetectionTypes.FindBiggestObject | HaarDetectionTypes.ScaleImage,
                new Size(minFace, minFace));

            if (faces.Length == 0) return false;

            // Several people: track the largest face, i.e. the closest person.
            Rect best = Biggest(faces);
            face = new FaceRect(best.X / k, best.Y / k, best.Width / k, best.Height / k);
            return true;
        }

        private static Rect Biggest(Rect[] faces)
        {
            Rect best = faces[0];
            for (int i = 1; i < faces.Length; i++)
                if (faces[i].Width > best.Width) best = faces[i];
            return best;
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

            DisposeMats();

            _bgr = new Mat(height, width, MatType.CV_8UC3);
            _gray = new Mat();
            _small = new Mat();
            _equalized = new Mat();
            _roiGray = new Mat();
            _roiSmall = new Mat();
            _roiEqualized = new Mat();

            _allocatedWidth = width;
            _allocatedHeight = height;
            ResetTracking();   // positions from another resolution mean nothing
        }

        private void DisposeMats()
        {
            _bgr?.Dispose();
            _gray?.Dispose();
            _small?.Dispose();
            _equalized?.Dispose();
            _roiGray?.Dispose();
            _roiSmall?.Dispose();
            _roiEqualized?.Dispose();
        }

        public void Dispose()
        {
            _cascade.Dispose();
            DisposeMats();
        }
    }
}