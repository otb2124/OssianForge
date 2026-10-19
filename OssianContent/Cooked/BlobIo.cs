using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace OssianForge.Content.Cooked;

/// <summary>Little-endian blob writer. Blittable arrays are written as raw memory.</summary>
public sealed class BlobWriter
{
    static BlobWriter()
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Ossian cooked formats are little-endian only.");
    }

    private readonly MemoryStream _ms;

    public BlobWriter(int capacity = 1024) => _ms = new MemoryStream(capacity);

    public void Header(uint magic, ushort version) { U32(magic); U16(version); }

    public void U8(byte v) => _ms.WriteByte(v);

    public void U16(ushort v)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, v);
        _ms.Write(b);
    }

    public void U32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    public void I32(int v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    public void F32(float v) => I32(BitConverter.SingleToInt32Bits(v));

    public void F64(double v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(b, v);
        _ms.Write(b);
    }

    public void Str(string? s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
        I32(bytes.Length);
        _ms.Write(bytes);
    }

    public void Struct<T>(in T v) where T : unmanaged
        => _ms.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in v), 1)));

    /// <summary>i32 count followed by the raw element bytes.</summary>
    public void Arr<T>(ReadOnlySpan<T> v) where T : unmanaged
    {
        I32(v.Length);
        _ms.Write(MemoryMarshal.AsBytes(v));
    }

    public byte[] ToArray() => _ms.ToArray();
}

public sealed class BlobReader
{
    private readonly byte[] _d;
    private int _p;

    public BlobReader(byte[] data) => _d = data;

    public void ExpectHeader(uint magic, ushort version, string what)
    {
        if (_d.Length < 6 || U32() != magic)
            throw new InvalidDataException($"Not a cooked {what} blob (bad magic).");
        ushort v = U16();
        if (v != version)
            throw new InvalidDataException($"Unsupported cooked {what} version {v} (expected {version}).");
    }

    public byte U8() => _d[_p++];

    public ushort U16() { var v = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(_p)); _p += 2; return v; }

    public uint U32() { var v = BinaryPrimitives.ReadUInt32LittleEndian(_d.AsSpan(_p)); _p += 4; return v; }

    public int I32() { var v = BinaryPrimitives.ReadInt32LittleEndian(_d.AsSpan(_p)); _p += 4; return v; }

    public float F32() => BitConverter.Int32BitsToSingle(I32());

    public double F64() { var v = BinaryPrimitives.ReadDoubleLittleEndian(_d.AsSpan(_p)); _p += 8; return v; }

    public string Str()
    {
        int n = I32();
        if (n < 0) throw new InvalidDataException("Negative string length.");
        string s = Encoding.UTF8.GetString(_d, _p, n);
        _p += n;
        return s;
    }

    public T Struct<T>() where T : unmanaged
    {
        T v = MemoryMarshal.Read<T>(_d.AsSpan(_p));
        _p += Unsafe.SizeOf<T>();
        return v;
    }

    public T[] Arr<T>() where T : unmanaged
    {
        int n = I32();
        if (n < 0) throw new InvalidDataException("Negative array length.");
        var arr = new T[n];
        var bytes = MemoryMarshal.AsBytes(arr.AsSpan());
        _d.AsSpan(_p, bytes.Length).CopyTo(bytes);
        _p += bytes.Length;
        return arr;
    }
}
