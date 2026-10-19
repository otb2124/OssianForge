using OssianForge.Content.Cooked;

namespace OssianForge.Content.Textures;

public enum TexturePixelFormat : byte
{
    /// <summary>Uncompressed 8-bit RGBA, 4 bytes per pixel.</summary>
    Rgba8 = 0,
    // Reserved for the BCn step: Bc1 = 1, Bc5 = 2, Bc7 = 3
}

public sealed class TextureData
{
    public int Width;
    public int Height;
    public TexturePixelFormat Format;
    public bool Srgb;
    /// <summary>Mip 0 first. Mip i has size max(1, Width >> i) x max(1, Height >> i). Rows are already flipped if the import asked for it.</summary>
    public byte[][] Mips = Array.Empty<byte[]>();

    public static int MipWidth(int width, int level) => Math.Max(1, width >> level);
    public static int MipHeight(int height, int level) => Math.Max(1, height >> level);
}

/// <summary>Cooked texture format "OTEX" v1.</summary>
public static class TextureCodec
{
    public const uint Magic = 0x5845544F; // 'O','T','E','X'
    public const ushort Version = 1;

    public static byte[] Write(TextureData t)
    {
        long total = 0;
        foreach (var m in t.Mips) total += m.Length;

        var w = new BlobWriter((int)Math.Min(total + 64, int.MaxValue / 2));
        w.Header(Magic, Version);
        w.I32(t.Width);
        w.I32(t.Height);
        w.U8((byte)t.Format);
        w.U8((byte)(t.Srgb ? 1 : 0));
        w.U8((byte)t.Mips.Length);
        foreach (var mip in t.Mips) w.Arr<byte>(mip);
        return w.ToArray();
    }

    public static TextureData Read(byte[] blob)
    {
        var r = new BlobReader(blob);
        r.ExpectHeader(Magic, Version, "texture");

        var t = new TextureData
        {
            Width = r.I32(),
            Height = r.I32(),
            Format = (TexturePixelFormat)r.U8(),
            Srgb = (r.U8() & 1) != 0
        };
        t.Mips = new byte[r.U8()][];
        for (int i = 0; i < t.Mips.Length; i++) t.Mips[i] = r.Arr<byte>();
        return t;
    }
}
