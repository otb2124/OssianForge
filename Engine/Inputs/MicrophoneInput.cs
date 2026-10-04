using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;

namespace OssianForge.Engine.Inputs
{
    public sealed class MicrophoneInputOptions
    {
        /// <summary>null or empty = system default capture device; otherwise the exact OpenAL device name.</summary>
        public string? DeviceName = null;

        public int SampleRate = 44100;

        /// <summary>How much recent audio the engine keeps for analyzers (FFT, voice detection, ...).</summary>
        public float RingSeconds = 2f;

        /// <summary>Size of OpenAL's own capture buffer. It covers frames where Update() stalls.</summary>
        public float DeviceBufferSeconds = 1f;

        /// <summary>Level smoothing times for SmoothedLevel, in seconds.</summary>
        public float AttackSeconds = 0.01f;
        public float ReleaseSeconds = 0.15f;

        /// <summary>Opening a microphone can show an OS "in use" indicator, so nothing opens until Start().</summary>
        public bool AutoStart = false;
    }

    /// <summary>
    /// Microphone input on top of the OpenAL capture API the engine already uses for playback.
    /// OpenAL capture is non-blocking and buffers on the device side, so unlike CameraInput
    /// this needs no thread: Update() drains whatever has arrived since the last frame.
    /// All members are main-thread only.
    /// </summary>
    public sealed unsafe class MicrophoneInput
    {
        // ALC_CAPTURE_SAMPLES from the OpenAL spec; Silk's GetContextInteger enum has no member for it.
        private const int AlcCaptureSamples = 0x312;

        private static Lazy<MicrophoneInput> LazyInstance = new(() => new MicrophoneInput());
        public static MicrophoneInput Instance => LazyInstance.Value;

        private MicrophoneInputOptions _options = new();

        private ALContext? _alc;
        private Capture? _capture;
        private Device* _device;

        private float[] _ring = Array.Empty<float>();
        private short[] _scratch = new short[4096];
        private long _totalSamples;

        public bool IsRunning { get; private set; }
        public string? LastError { get; private set; }
        public int SampleRate => _options.SampleRate;

        /// <summary>RMS of the audio that arrived in the latest Update(), 0..1.</summary>
        public float Rms { get; private set; }

        /// <summary>Peak absolute sample of the latest Update(), 0..1.</summary>
        public float Peak { get; private set; }

        /// <summary>Rms with fast attack and slow release, good for driving visuals.</summary>
        public float SmoothedLevel { get; private set; }

        public float LevelDb => Rms > 1e-6f ? 20f * MathF.Log10(Rms) : -120f;

        /// <summary>Total samples captured since Start(). Use with CopySince().</summary>
        public long TotalSamples => _totalSamples;

        private MicrophoneInput() { }

        public void Initialize(MicrophoneInputOptions options)
        {
            _options = options ?? new MicrophoneInputOptions();
            if (_options.AutoStart) Start();
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        /// <summary>Opens the device and starts capturing. Returns false (and sets LastError) on failure.</summary>
        public bool Start()
        {
            if (IsRunning) return true;

            var alc = Engine.Audio?.AudioSystem?.ALC;
            if (alc == null)
                return Fail("Audio system is not initialized yet; call Start() after Engine.Initialize().");

            if (!alc.TryGetExtension<Capture>(null, out var capture))
                return Fail("OpenAL capture extension (ALC_EXT_CAPTURE) is not available.");

            int deviceBufferSamples = Math.Max(1024, (int)(_options.SampleRate * _options.DeviceBufferSeconds));

            var device = capture.CaptureOpenDevice(
                _options.DeviceName ?? string.Empty,
                (uint)_options.SampleRate,
                BufferFormat.Mono16,
                deviceBufferSamples);

            if (device == null)
                return Fail($"Could not open microphone '{_options.DeviceName ?? "default"}' (ALC error {alc.GetError(null)}).");

            capture.CaptureStart(device);

            _alc = alc;
            _capture = capture;
            _device = device;

            _ring = new float[Math.Max(1024, (int)(_options.SampleRate * _options.RingSeconds))];
            _totalSamples = 0;
            Rms = Peak = SmoothedLevel = 0f;
            LastError = null;
            IsRunning = true;

            Console.WriteLine($"[INPUT] Microphone opened ({_options.DeviceName ?? "default"}, {_options.SampleRate} Hz).");
            return true;
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _capture!.CaptureStop(_device);
            _capture.CaptureCloseDevice(_device);

            _device = null;
            _capture = null;
            _alc = null;
            IsRunning = false;
            Rms = Peak = SmoothedLevel = 0f;
        }

        /// <summary>Call when the window closes.</summary>
        public void Shutdown() => Stop();

        /// <summary>Switch device or sample rate; restarts capture if it was running.</summary>
        public void Reconfigure(MicrophoneInputOptions options)
        {
            bool wasRunning = IsRunning;
            Stop();
            _options = options;
            if (wasRunning || options.AutoStart) Start();
        }

        // ── Per-frame update ─────────────────────────────────────────────────

        public void Update(double delta)
        {
            if (!IsRunning) return;

            int available = 0;
            _alc!.GetContextProperty(_device, (GetContextInteger)AlcCaptureSamples, 1, &available);

            double sumSquares = 0;
            float peak = 0f;
            int count = 0;

            while (available > 0)
            {
                int n = Math.Min(available, _scratch.Length);

                fixed (short* ptr = _scratch)
                    _capture!.CaptureSamples(_device, ptr, n);

                for (int i = 0; i < n; i++)
                {
                    float s = _scratch[i] / 32768f;
                    _ring[(int)(_totalSamples % _ring.Length)] = s;
                    _totalSamples++;

                    sumSquares += s * s;
                    float abs = MathF.Abs(s);
                    if (abs > peak) peak = abs;
                }

                count += n;
                available -= n;
            }

            // Keep the previous level on frames where no samples happened to arrive.
            if (count > 0)
            {
                Rms = MathF.Sqrt((float)(sumSquares / count));
                Peak = peak;
            }

            float tau = Rms > SmoothedLevel ? _options.AttackSeconds : _options.ReleaseSeconds;
            float alpha = tau <= 0f ? 1f : 1f - MathF.Exp(-(float)delta / tau);
            SmoothedLevel += (Rms - SmoothedLevel) * alpha;
        }

        // ── Reading samples ──────────────────────────────────────────────────

        /// <summary>
        /// Copies samples captured since cursor, advancing it. Start with cursor = TotalSamples
        /// (or 0 for everything buffered). If you fall more than RingSeconds behind, the
        /// oldest samples are skipped. Returns how many samples were written.
        /// </summary>
        public int CopySince(ref long cursor, Span<float> destination)
        {
            long capacity = _ring.Length;
            if (capacity == 0) return 0;

            if (cursor > _totalSamples) cursor = _totalSamples;
            if (cursor < _totalSamples - capacity) cursor = _totalSamples - capacity;

            int n = (int)Math.Min(destination.Length, _totalSamples - cursor);
            if (n <= 0) return 0;

            int start = (int)(cursor % capacity);
            int first = Math.Min(n, _ring.Length - start);

            _ring.AsSpan(start, first).CopyTo(destination);
            if (n > first)
                _ring.AsSpan(0, n - first).CopyTo(destination.Slice(first));

            cursor += n;
            return n;
        }

        /// <summary>
        /// Copies the most recent samples (oldest first), up to destination.Length.
        /// Returns how many were available. Handy for FFT windows and one-shot analysis.
        /// </summary>
        public int ReadRecent(Span<float> destination)
        {
            int n = (int)Math.Min(Math.Min(destination.Length, _totalSamples), _ring.Length);
            if (n <= 0) return 0;

            long cursor = _totalSamples - n;
            return CopySince(ref cursor, destination.Slice(0, n));
        }

        private bool Fail(string message)
        {
            LastError = message;
            Console.WriteLine($"[INPUT] {message}");
            return false;
        }
    }
}