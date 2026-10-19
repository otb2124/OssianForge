namespace OssianForge.Content.Pack;

/// <summary>Reads cooked blobs from a pack. Used in release builds.</summary>
public sealed class PackContentSource : IContentSource
{
    private readonly PackReader _reader;

    public PackContentSource(string packPath) => _reader = new PackReader(packPath);

    public bool IsCooked => true;

    public PackReader Reader => _reader;

    public bool Exists(string path) => _reader.TryGetEntry(path, out _);

    public bool TryGetType(string path, out AssetType type)
    {
        bool ok = _reader.TryGetEntry(path, out var e);
        type = ok ? e.Type : default;
        return ok;
    }

    public byte[] ReadAllBytes(string path) => _reader.Read(path);

    public Stream Open(string path) => new MemoryStream(_reader.Read(path), writable: false);

    public IEnumerable<string> List(string folder)
    {
        string prefix = PathUtil.Normalize(folder).TrimEnd('/');
        if (prefix.Length > 0) prefix += "/";
        foreach (var e in _reader.Entries)
            if (PathUtil.Normalize(e.Name).StartsWith(prefix, StringComparison.Ordinal))
                yield return e.Name;
    }

    public void Dispose() => _reader.Dispose();
}
