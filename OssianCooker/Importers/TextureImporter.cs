using OssianForge.Content;
using OssianForge.Content.Textures;
using StbImageSharp;

namespace OssianForge.Cooker.Importers;

/// <summary>
/// Decodes PNG/JPG with stb into RGBA8 and precomputes the mip chain (what GenerateMipmap did at runtime).
/// Defaults match TextureFile: flipped vertically, with mips. Cubemap faces / heightmaps get different defaults via hints.
/// The cooked format has a pixel-format field reserved for BC1/BC5/BC7; this version only emits Rgba8.
/// </summary>
internal sealed class TextureImporter : IImporter
{
    public AssetType Type => AssetType.Texture;
    public int Version => 1;

    static TextureImporter()
    {
        // We flip rows ourselves so that the (global, non-thread-safe) stb flag never matters.
        StbImage.stbi_set_flip_vertically_on_load(0);
    }

    public byte[] Import(ImportContext ctx)
    {
        bool flip = ctx.Settings.FlipY ?? true;
        bool mips = ctx.Settings.Mips ?? true;
        bool srgb = ctx.Settings.Srgb ?? false;

        ImageResult image;
        using (var ms = new MemoryStream(ctx.ReadSource()))
            image = ImageResult.FromStream(ms, ColorComponents.RedGreenBlueAlpha);

        int w = image.Width, h = image.Height;
        byte[] level0 = image.Data;
        if (flip) FlipRows(level0, w, h);

        var chain = new List<byte[]> { level0 };
        if (mips)
        {
            byte[] cur = level0;
            int cw = w, ch = h;
            while (cw > 1 || ch > 1)
            {
                cur = Downsample(cur, cw, ch, out int nw, out int nh);
                chain.Add(cur);
                cw = nw; ch = nh;
            }
        }

        return TextureCodec.Write(new TextureData
        {
            Width = w,
            Height = h,
            Format = TexturePixelFormat.Rgba8,
            Srgb = srgb,
            Mips = chain.ToArray()
        });
    }

    private static void FlipRows(byte[] data, int w, int h)
    {
        int stride = w * 4;
        var tmp = new byte[stride];
        for (int y = 0; y < h / 2; y++)
        {
            int a = y * stride, b = (h - 1 - y) * stride;
            Buffer.BlockCopy(data, a, tmp, 0, stride);
            Buffer.BlockCopy(data, b, data, a, stride);
            Buffer.BlockCopy(tmp, 0, data, b, stride);
        }
    }

    /// <summary>2x2 box filter (same space as GenerateMipmap on a non-sRGB texture: no gamma conversion).</summary>
    private static byte[] Downsample(byte[] src, int w, int h, out int nw, out int nh)
    {
        nw = Math.Max(1, w / 2);
        nh = Math.Max(1, h / 2);
        var dst = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
        {
            int y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
            for (int x = 0; x < nw; x++)
            {
                int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1);
                for (int c = 0; c < 4; c++)
                {
                    int sum = src[(y0 * w + x0) * 4 + c] + src[(y0 * w + x1) * 4 + c]
                            + src[(y1 * w + x0) * 4 + c] + src[(y1 * w + x1) * 4 + c];
                    dst[(y * nw + x) * 4 + c] = (byte)((sum + 2) >> 2);
                }
            }
        }
        return dst;
    }
}
