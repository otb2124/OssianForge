using System.IO.Hashing;
using System.Text;

namespace OssianForge.Tools.Cook;

/// <summary>
/// Content-addressed cache of cooked blobs (pre-compression). Key = hash of source bytes + import settings +
/// importer version, so unchanged assets are never re-imported. Same idea as Unity's Library/ or Godot's .godot/imported/.
/// </summary>
internal sealed class BuildCache
{
    private readonly string? _dir;

    public BuildCache(string? dir) => _dir = dir;

    public static string MakeKey(string importer, int version, string normalizedPath, string settingsTag, string extra, byte[] source)
    {
        var h = new XxHash128();
        void Add(string s) { h.Append(Encoding.UTF8.GetBytes(s)); h.Append(new byte[] { 0 }); }
        Add(importer);
        Add(version.ToString());
        Add(normalizedPath);
        Add(settingsTag);
        Add(extra);
        h.Append(source);
        return Convert.ToHexString(h.GetCurrentHash()).ToLowerInvariant();
    }

    private string? PathFor(string key) => _dir == null ? null : Path.Combine(_dir, key[..2], key + ".bin");

    public bool TryGet(string key, out byte[] data)
    {
        data = Array.Empty<byte>();
        string? p = PathFor(key);
        if (p == null || !File.Exists(p)) return false;
        try { data = File.ReadAllBytes(p); return true; }
        catch (IOException) { return false; }
    }

    public void Put(string key, byte[] data)
    {
        string? p = PathFor(key);
        if (p == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            string tmp = p + "." + Environment.CurrentManagedThreadId + ".tmp";
            File.WriteAllBytes(tmp, data);
            File.Move(tmp, p, overwrite: true);
        }
        catch (IOException ex) { Log.Warn($"cache write failed: {ex.Message}"); }
    }

    public static int Clean(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        int n = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
        Directory.Delete(dir, recursive: true);
        return n;
    }
}
