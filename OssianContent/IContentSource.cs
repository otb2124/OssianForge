using System.Text;

namespace OssianForge.Content;

/// <summary>
/// Where the engine reads content from. Paths are relative to the Content folder
/// (the same strings the Resource classes already hold, e.g. "MeshFiles/remy.fbx").
/// </summary>
public interface IContentSource : IDisposable
{
    /// <summary>True when entries are cooked blobs (pack); false when they are the original source files.</summary>
    bool IsCooked { get; }

    bool Exists(string path);

    /// <summary>Whole entry as bytes. Throws <see cref="FileNotFoundException"/> if missing.</summary>
    byte[] ReadAllBytes(string path);

    Stream Open(string path);

    /// <summary>All entries below <paramref name="folder"/> (recursive), as content-relative paths.</summary>
    IEnumerable<string> List(string folder);
}

public static class ContentSourceExtensions
{
    public static string ReadAllText(this IContentSource source, string path)
        => new UTF8Encoding(false).GetString(StripBom(source.ReadAllBytes(path)));

    private static ReadOnlySpan<byte> StripBom(byte[] b)
        => b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? b.AsSpan(3) : b;
}

/// <summary>Loose files under a root folder. Used in dev and in the editor (hot reload keeps working).</summary>
public sealed class DiskContentSource : IContentSource
{
    private readonly string _root;

    public DiskContentSource(string rootFolder) => _root = Path.GetFullPath(rootFolder);

    public bool IsCooked => false;

    private string Full(string path) => Path.Combine(_root, PathUtil.NormalizeSlashes(path));

    public bool Exists(string path) => File.Exists(Full(path));

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(Full(path));

    public Stream Open(string path) => File.OpenRead(Full(path));

    public IEnumerable<string> List(string folder)
    {
        string dir = Full(folder);
        if (!Directory.Exists(dir)) yield break;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(_root, file).Replace('\\', '/');
    }

    public void Dispose() { }
}
