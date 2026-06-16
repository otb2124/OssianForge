using NVorbis;
using OssianForge.Engine.Audio;
using Silk.NET.OpenAL;
using System.Buffers.Binary;


namespace OssianForge.Engine.Resources.Sounds
{
    public class SoundFile : ResourceFile, IDisposable
    {
        // The OpenAL buffer handle — analogous to a VBO id.
        public uint BufferId { get; private set; }

        // Metadata exposed for SoundResource to query duration etc.
        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public double DurationSecs { get; private set; }

        public SoundFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            string fullPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;
            string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();

            byte[] pcmData;
            int sampleRate;
            int channels;
            BufferFormat format;

            if (ext == ".ogg")
                (pcmData, sampleRate, channels, format) = LoadOgg(fullPath);
            else if (ext == ".wav")
                (pcmData, sampleRate, channels, format) = LoadWav(fullPath);
            else
                throw new Exception($"[SOUND] Unsupported audio format '{ext}'. Use .wav or .ogg.");

            SampleRate = sampleRate;
            Channels = channels;

            // bytes per sample = 2 (16-bit PCM), total samples = pcmData.Length / 2 / channels
            DurationSecs = (double)pcmData.Length / (sampleRate * channels * 2);

            var al = Engine.Audio.AudioSystem.AL;
            BufferId = al.GenBuffer();

            unsafe
            {
                fixed (byte* ptr = pcmData)
                    al.BufferData(BufferId, format, ptr, pcmData.Length, sampleRate);
            }

            CheckError($"BufferData for '{Path}'");
        }

        // ── OGG via NVorbis ──────────────────────────────────────────────────

        private static (byte[] pcm, int rate, int ch, BufferFormat fmt) LoadOgg(string path)
        {
            using var reader = new VorbisReader(path);

            int channels = reader.Channels;
            int sampleRate = reader.SampleRate;
            var samples = new List<float>();
            var readBuf = new float[reader.Channels * 4096];
            int count;

            while ((count = reader.ReadSamples(readBuf, 0, readBuf.Length)) > 0)
                for (int i = 0; i < count; i++)
                    samples.Add(readBuf[i]);

            // Convert float [-1, 1] → signed 16-bit PCM
            var pcm = new byte[samples.Count * 2];
            for (int i = 0; i < samples.Count; i++)
            {
                short s = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), s);
            }

            var format = channels == 2 ? BufferFormat.Stereo16 : BufferFormat.Mono16;
            return (pcm, sampleRate, channels, format);
        }

        // ── WAV (PCM only) ───────────────────────────────────────────────────

        private static (byte[] pcm, int rate, int ch, BufferFormat fmt) LoadWav(string path)
        {
            using var fs = System.IO.File.OpenRead(path);
            using var reader = new System.IO.BinaryReader(fs);

            // RIFF header
            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF") throw new Exception($"[SOUND] Not a RIFF file: {path}");
            reader.ReadInt32(); // chunk size
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE") throw new Exception($"[SOUND] Not a WAVE file: {path}");

            int channels = 0;
            int sampleRate = 0;
            short bitsPerSample = 0;
            byte[] pcmData = Array.Empty<byte>();

            // Read chunks until we have fmt + data
            while (fs.Position < fs.Length - 8)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    short audioFormat = reader.ReadInt16();
                    if (audioFormat != 1)
                        throw new Exception($"[SOUND] Only PCM WAV supported (format={audioFormat}).");

                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();

                    // Skip any extra fmt bytes
                    int read = 16;
                    if (chunkSize > read) reader.ReadBytes(chunkSize - read);
                }
                else if (chunkId == "data")
                {
                    pcmData = reader.ReadBytes(chunkSize);
                }
                else
                {
                    reader.ReadBytes(chunkSize); // skip unknown chunks (LIST, id3, etc.)
                }
            }

            if (pcmData.Length == 0)
                throw new Exception($"[SOUND] No data chunk found in WAV: {path}");

            BufferFormat format = (channels, bitsPerSample) switch
            {
                (1, 8) => BufferFormat.Mono8,
                (1, 16) => BufferFormat.Mono16,
                (2, 8) => BufferFormat.Stereo8,
                (2, 16) => BufferFormat.Stereo16,
                _ => throw new Exception($"[SOUND] Unsupported WAV format: {channels}ch {bitsPerSample}bit")
            };

            return (pcmData, sampleRate, channels, format);
        }

        private static void CheckError(string context)
        {
            var err = Engine.Audio.AudioSystem.AL.GetError();
            if (err != AudioError.NoError)
                Console.WriteLine($"[AUDIO ERROR] {context}: {err}");
        }

        public void Dispose()
        {
            if (BufferId != 0)
                Engine.Audio.AudioSystem.AL.DeleteBuffer(BufferId);
        }
    }
}