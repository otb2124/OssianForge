namespace OssianForge.Content.Pack;

/// <summary>
/// Pack layout (little-endian):
///   Header (32 bytes): magic "OSSN" u32 | version u16 | reserved u16 | entryCount u32 | reserved u32 | tocOffset u64 | tocSize u64
///   Blobs: each 16-byte aligned, optionally LZ4-compressed
///   TOC (variable): per entry  idHash u64 | offset u64 | storedSize u32 | rawSize u32 | type u8 | flags u8 | nameLen u16 | name utf8
/// idHash = XxHash64 of the lower-cased, '/'-separated content-relative path.
/// </summary>
public static class PackFormat
{
    public const uint Magic = 0x4E53534F; // bytes 'O','S','S','N' on disk
    public const ushort Version = 1;
    public const int HeaderSize = 32;
    public const int Alignment = 16;
    public const byte FlagLz4 = 1;
}

public readonly record struct PackEntry(
    ulong Hash, long Offset, int StoredSize, int RawSize, AssetType Type, byte Flags, string Name)
{
    public bool IsCompressed => (Flags & PackFormat.FlagLz4) != 0;
}
