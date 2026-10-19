using K4os.Compression.LZ4;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace OssianForge.Content.Pack;

/// <summary>Writes a pack to "&lt;path&gt;.tmp" and moves it into place on <see cref="Complete"/>.</summary>
public sealed class PackWriter : IDisposable
{
    private readonly string _finalPath;
    private readonly string _tmpPath;
    private readonly FileStream _fs;
    private readonly bool _compress;
    private readonly List<PackEntry> _entries = new();
    private readonly HashSet<ulong> _hashes = new();
    private bool _completed;

    public PackWriter(string path, bool compress = true)
    {
        _finalPath = Path.GetFullPath(path);
        _tmpPath = _finalPath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_finalPath)!);
        _fs = new FileStream(_tmpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 16);
        _compress = compress;
        _fs.Write(new byte[PackFormat.HeaderSize]); // placeholder, rewritten in Complete()
    }

    public int Count => _entries.Count;
    public long Length => _fs.Length;

    public void Add(string path, AssetType type, ReadOnlySpan<byte> data)
    {
        string name = PathUtil.NormalizeSlashes(path);
        ulong hash = PathUtil.Hash(name.ToLowerInvariant());
        if (!_hashes.Add(hash))
            throw new InvalidOperationException($"Duplicate (or hash-colliding) pack entry: '{name}'");
        if (Encoding.UTF8.GetByteCount(name) > ushort.MaxValue)
            throw new InvalidOperationException($"Entry name too long: '{name}'");

        Align();
        long offset = _fs.Position;
        byte flags = 0;
        int stored = data.Length;

        if (_compress && data.Length >= 64)
        {
            int max = LZ4Codec.MaximumOutputSize(data.Length);
            byte[] buf = ArrayPool<byte>.Shared.Rent(max);
            try
            {
                int n = LZ4Codec.Encode(data, buf.AsSpan(0, max), LZ4Level.L09_HC);
                if (n > 0 && (long)n * 10 < (long)data.Length * 9)
                {
                    _fs.Write(buf, 0, n);
                    flags |= PackFormat.FlagLz4;
                    stored = n;
                }
                else _fs.Write(data);
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }
        else _fs.Write(data);

        _entries.Add(new PackEntry(hash, offset, stored, data.Length, type, flags, name));
    }

    public void Complete()
    {
        if (_completed) return;

        Align();
        long tocOffset = _fs.Position;

        var toc = new MemoryStream();
        Span<byte> tmp = stackalloc byte[8];
        foreach (var e in _entries)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(tmp, e.Hash); toc.Write(tmp);
            BinaryPrimitives.WriteInt64LittleEndian(tmp, e.Offset); toc.Write(tmp);
            BinaryPrimitives.WriteInt32LittleEndian(tmp, e.StoredSize); toc.Write(tmp[..4]);
            BinaryPrimitives.WriteInt32LittleEndian(tmp, e.RawSize); toc.Write(tmp[..4]);
            toc.WriteByte((byte)e.Type);
            toc.WriteByte(e.Flags);
            byte[] nameBytes = Encoding.UTF8.GetBytes(e.Name);
            BinaryPrimitives.WriteUInt16LittleEndian(tmp, (ushort)nameBytes.Length); toc.Write(tmp[..2]);
            toc.Write(nameBytes);
        }
        _fs.Write(toc.GetBuffer(), 0, (int)toc.Length);

        Span<byte> header = stackalloc byte[PackFormat.HeaderSize];
        header.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(header[0..], PackFormat.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], PackFormat.Version);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)_entries.Count);
        BinaryPrimitives.WriteUInt64LittleEndian(header[16..], (ulong)tocOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(header[24..], (ulong)toc.Length);
        _fs.Position = 0;
        _fs.Write(header);

        _fs.Flush(true);
        _fs.Dispose();
        File.Move(_tmpPath, _finalPath, overwrite: true);
        _completed = true;
    }

    private void Align()
    {
        int pad = (int)((PackFormat.Alignment - (_fs.Position % PackFormat.Alignment)) % PackFormat.Alignment);
        if (pad > 0) _fs.Write(new byte[pad]);
    }

    public void Dispose()
    {
        if (_completed) return;
        _fs.Dispose();
        try { File.Delete(_tmpPath); } catch { /* best effort */ }
    }
}
