using NVorbis;
using OssianForge.Content;
using OssianForge.Content.Sounds;
using System.Buffers.Binary;

namespace OssianForge.Cooker.Importers;

/// <summary>
/// Decodes .ogg (NVorbis) and PCM .wav into interleaved PCM once, streaming into the output
/// instead of building a List&lt;float&gt; first. Long music should later be streamed from OGG instead.
/// </summary>
internal sealed class SoundImporter : IImporter
{
    public AssetType Type => AssetType.Sound;
    public int Version => 1;

    public byte[] Import(ImportContext ctx)
    {
        string ext = Path.GetExtension(ctx.FullPath).ToLowerInvariant();
        SoundData data = ext switch
        {
            ".ogg" => LoadOgg(ctx.FullPath),
            ".wav" => LoadWav(ctx.FullPath),
            _ => throw new Exception($"Unsupported audio format '{ext}'. Use .wav or .ogg.")
        };

        if (data.Channels is < 1 or > 2)
            throw new Exception($"Unsupported channel count {data.Channels} (mono or stereo only).");

        return SoundCodec.Write(data);
    }

    private static SoundData LoadOgg(string path)
    {
        using var reader = new VorbisReader(path);
        int channels = reader.Channels;

        var pcm = new MemoryStream();
        var readBuf = new float[channels * 4096];
        var outBuf = new byte[readBuf.Length * 2];
        int count;
        while ((count = reader.ReadSamples(readBuf, 0, readBuf.Length)) > 0)
        {
            for (int i = 0; i < count; i++)
            {
                short s = (short)Math.Clamp(readBuf[i] * 32767f, -32768f, 32767f);
                BinaryPrimitives.WriteInt16LittleEndian(outBuf.AsSpan(i * 2), s);
            }
            pcm.Write(outBuf, 0, count * 2);
        }

        return new SoundData
        {
            SampleRate = reader.SampleRate,
            Channels = channels,
            BitsPerSample = 16,
            Pcm = pcm.ToArray()
        };
    }

    private static SoundData LoadWav(string path)
    {
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);

        if (new string(reader.ReadChars(4)) != "RIFF") throw new Exception("Not a RIFF file.");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE") throw new Exception("Not a WAVE file.");

        int channels = 0, sampleRate = 0;
        short bits = 0;
        byte[] pcm = Array.Empty<byte>();

        while (fs.Position < fs.Length - 8)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            long chunkEnd = fs.Position + chunkSize + (chunkSize & 1); // chunks are word-aligned

            if (chunkId == "fmt ")
            {
                short audioFormat = reader.ReadInt16();
                if (audioFormat != 1) throw new Exception($"Only PCM WAV supported (format={audioFormat}).");
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bits = reader.ReadInt16();
            }
            else if (chunkId == "data")
            {
                pcm = reader.ReadBytes(chunkSize);
            }

            fs.Position = Math.Min(chunkEnd, fs.Length);
        }

        if (pcm.Length == 0) throw new Exception("No data chunk found in WAV.");
        if (bits is not (8 or 16)) throw new Exception($"Unsupported WAV bit depth: {bits}.");

        return new SoundData { SampleRate = sampleRate, Channels = channels, BitsPerSample = bits, Pcm = pcm };
    }
}
