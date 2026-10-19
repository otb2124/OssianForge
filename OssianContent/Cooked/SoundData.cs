using OssianForge.Content.Cooked;

namespace OssianForge.Content.Sounds;

public sealed class SoundData
{
    public int SampleRate;
    public int Channels;
    /// <summary>8 or 16. Samples are interleaved little-endian PCM (8-bit unsigned, 16-bit signed), as OpenAL expects.</summary>
    public int BitsPerSample;
    public byte[] Pcm = Array.Empty<byte>();

    public double DurationSeconds
        => (double)Pcm.Length / (SampleRate * Channels * (BitsPerSample / 8));
}

/// <summary>Cooked sound format "OSND" v1 (decoded PCM, for short SFX).</summary>
public static class SoundCodec
{
    public const uint Magic = 0x444E534F; // 'O','S','N','D'
    public const ushort Version = 1;

    public static byte[] Write(SoundData s)
    {
        var w = new BlobWriter(s.Pcm.Length + 64);
        w.Header(Magic, Version);
        w.I32(s.SampleRate);
        w.U8((byte)s.Channels);
        w.U8((byte)s.BitsPerSample);
        w.Arr<byte>(s.Pcm);
        return w.ToArray();
    }

    public static SoundData Read(byte[] blob)
    {
        var r = new BlobReader(blob);
        r.ExpectHeader(Magic, Version, "sound");
        return new SoundData
        {
            SampleRate = r.I32(),
            Channels = r.U8(),
            BitsPerSample = r.U8(),
            Pcm = r.Arr<byte>()
        };
    }
}
