using OssianForge.Content;
using OssianForge.Content.Animations;
using OssianForge.Content.Meshes;
using OssianForge.Content.Pack;
using OssianForge.Content.Sounds;
using OssianForge.Content.Textures;

namespace OssianForge.Cooker;

internal static class Commands
{
    private static string PackArg(CookOptions o) => o.Positional.Count > 0 ? o.Positional[0] : o.Output;

    public static int List(CookOptions o)
    {
        string path = PackArg(o);
        if (!File.Exists(path)) { Log.Error(path, "pack not found"); return 2; }

        using var pack = new PackReader(path);
        Console.WriteLine($"{pack.Entries.Count} entries in {pack.Path}");
        foreach (var e in pack.Entries)
            Console.WriteLine($"{e.Type,-10} {Log.FormatSize(e.RawSize),10} {(e.IsCompressed ? "lz4 " + Log.FormatSize(e.StoredSize) : ""),-14} {e.Name}");
        return 0;
    }

    /// <summary>Reads (and decompresses) every entry and parses every cooked blob with the shared codecs.</summary>
    public static int Verify(CookOptions o)
    {
        string path = PackArg(o);
        if (!File.Exists(path)) { Log.Error(path, "pack not found"); return 2; }

        int bad = 0;
        using var pack = new PackReader(path);
        foreach (var e in pack.Entries)
        {
            try
            {
                byte[] data = pack.Read(e);
                if (data.Length != e.RawSize) throw new InvalidDataException("size mismatch");
                switch (e.Type)
                {
                    case AssetType.Mesh: MeshCodec.Read(data); break;
                    case AssetType.Animation: AnimationCodec.Read(data); break;
                    case AssetType.Texture: TextureCodec.Read(data); break;
                    case AssetType.Sound: SoundCodec.Read(data); break;
                }
            }
            catch (Exception ex)
            {
                bad++;
                Log.Error(e.Name, ex.Message);
            }
        }

        Log.Info(bad == 0 ? $"OK: {pack.Entries.Count} entries verified." : $"{bad} bad entr{(bad == 1 ? "y" : "ies")}.");
        return bad == 0 ? 0 : 1;
    }

    public static int Clean(CookOptions o)
    {
        string dir = o.CacheDir ?? CookRunner.DefaultCacheDir(Path.GetFullPath(o.ContentDir));
        int n = BuildCache.Clean(dir);
        Log.Info($"Removed {n} cached file(s) from {dir}");
        return 0;
    }
}
