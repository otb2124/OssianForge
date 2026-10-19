using K4os.Compression.LZ4;
using System.Buffers.Binary;
using System.Text;

namespace OssianForge.Content.Pack;

/// <summary>Random-access pack reader. Thread-safe (positional reads, no shared seek state).</summary>
public sealed class PackReader : IDisposable
{
    private readonly FileStream _fs;
    private readonly Dictionary<ulong, PackEntry> _byHash = new();
    private readonly List<PackEntry> _entries = new();

    public string Path { get; }
    public IReadOnlyList<PackEntry> Entries => _entries;

    public PackReader(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        _fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 0, FileOptions.RandomAccess);

        Span<byte> header = stackalloc byte[PackFormat.HeaderSize];
        ReadExactly(0, header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != PackFormat.Magic)
            throw new InvalidDataException($"Not an Ossian pack: {Path}");
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        if (version != PackFormat.Version)
            throw new InvalidDataException($"Unsupported pack version {version} (expected {PackFormat.Version}): {Path}");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        long tocOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(header[16..]);
        long tocSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(header[24..]);
        if (tocOffset < PackFormat.HeaderSize || tocSize < 0 || tocOffset + tocSize > _fs.Length)
            throw new InvalidDataException($"Corrupt pack header: {Path}");

        byte[] toc = new byte[tocSize];
        ReadExactly(tocOffset, toc);

        int p = 0;
        for (uint i = 0; i < count; i++)
        {
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(toc.AsSpan(p)); p += 8;
            long offset = BinaryPrimitives.ReadInt64LittleEndian(toc.AsSpan(p)); p += 8;
            int stored = BinaryPrimitives.ReadInt32LittleEndian(toc.AsSpan(p)); p += 4;
            int raw = BinaryPrimitives.ReadInt32LittleEndian(toc.AsSpan(p)); p += 4;
            var type = (AssetType)toc[p++];
            byte flags = toc[p++];
            int nameLen = BinaryPrimitives.ReadUInt16LittleEndian(toc.AsSpan(p)); p += 2;
            string name = Encoding.UTF8.GetString(toc, p, nameLen); p += nameLen;

            var entry = new PackEntry(hash, offset, stored, raw, type, flags, name);
            _entries.Add(entry);
            _byHash[hash] = entry;
        }
    }

    public bool TryGetEntry(string path, out PackEntry entry)
    {
        string key = PathUtil.Normalize(path);
        if (_byHash.TryGetValue(PathUtil.Hash(key), out entry) && PathUtil.Normalize(entry.Name) == key)
            return true;
        entry = default;
        return false;
    }

    public byte[] Read(string path)
    {
        if (!TryGetEntry(path, out var entry))
            throw new FileNotFoundException($"Not in pack: '{path}'", path);
        return Read(entry);
    }

    public byte[] Read(in PackEntry entry)
    {
        byte[] stored = new byte[entry.StoredSize];
        ReadExactly(entry.Offset, stored);
        if (!entry.IsCompressed) return stored;

        byte[] raw = new byte[entry.RawSize];
        int n = LZ4Codec.Decode(stored, raw);
        if (n != entry.RawSize)
            throw new InvalidDataException($"LZ4 size mismatch for '{entry.Name}' ({n} != {entry.RawSize})");
        return raw;
    }

    private void ReadExactly(long offset, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = RandomAccess.Read(_fs.SafeFileHandle, buffer[total..], offset + total);
            if (n <= 0) throw new EndOfStreamException($"Unexpected end of pack: {Path}");
            total += n;
        }
    }

    public void Dispose() => _fs.Dispose();
}
