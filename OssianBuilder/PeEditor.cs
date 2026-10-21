using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OssianForge.Builder;

/// <summary>
/// Edits a Windows exe in place: icon, version info and the console/GUI subsystem flag.
/// Uses the Win32 UpdateResource API (the same mechanism rcedit and the .NET SDK use), so it needs no extra tool.
/// Only for a normal multi-file apphost. A single-file bundle keeps its payload after the PE image and must not
/// be patched this way (the Packager refuses such a template).
/// </summary>
internal static class PeEditor
{
    private static readonly IntPtr RT_ICON = (IntPtr)3;
    private static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
    private static readonly IntPtr RT_VERSION = (IntPtr)16;
    private const ushort Lang = 1033; // en-US, matches the "040904b0" string table below

    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

    // ── Win32 ────────────────────────────────────────────────────────────────

    private delegate bool EnumNameProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam);
    private delegate bool EnumLangProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLanguage, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResourceW(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResourceW(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[]? lpData, uint cb);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResourceW(IntPtr hUpdate, bool fDiscard);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceNamesW(IntPtr hModule, IntPtr lpType, EnumNameProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceLanguagesW(IntPtr hModule, IntPtr lpType, IntPtr lpName, EnumLangProc lpEnumFunc, IntPtr lParam);

    // ── public API ───────────────────────────────────────────────────────────

    public sealed class IconImage
    {
        public byte Width, Height, ColorCount;
        public ushort Planes, BitCount;
        public byte[] Data = Array.Empty<byte>();
    }

    public sealed class VersionInfo
    {
        public string Version = "1.0.0.0";
        public string ProductName = "";
        public string FileDescription = "";
        public string CompanyName = "";
        public string LegalCopyright = "";
        public string OriginalFilename = "";
        public string InternalName = "";
    }

    /// <summary>Loads an .ico, or wraps a .png (at most 256x256) into a one-image icon.</summary>
    public static List<IconImage> LoadIcon(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".ico" => ParseIco(file, path),
            ".png" => new List<IconImage> { FromPng(file, path) },
            _ => throw new ArgumentException($"Icon must be .ico or .png: {path}")
        };
    }

    /// <summary>Sets the PE subsystem: GUI (no console window) or console.</summary>
    public static void SetSubsystem(string exePath, bool gui)
    {
        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var br = new BinaryReader(fs);
        var bw = new BinaryWriter(fs);

        fs.Position = 0x3C;
        int pe = br.ReadInt32();
        fs.Position = pe;
        if (br.ReadUInt32() != 0x00004550) throw new InvalidDataException($"{exePath}: not a PE file.");

        // Optional header starts 24 bytes after the PE signature; Subsystem sits at +68 in both PE32 and PE32+.
        fs.Position = pe + 24 + 68;
        bw.Write((ushort)(gui ? 2 : 3));
    }

    /// <summary>Replaces the exe's icon and/or version resources. Other resources (manifest...) are kept.</summary>
    public static void Patch(string exePath, List<IconImage>? icon, VersionInfo? version)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Icon and version patching needs Windows.");

        var types = new List<IntPtr>();
        if (icon != null) { types.Add(RT_ICON); types.Add(RT_GROUP_ICON); }
        if (version != null) types.Add(RT_VERSION);
        if (types.Count == 0) return;

        var old = Enumerate(exePath, types);
        IntPtr h = BeginUpdateResourceW(exePath, false);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open '{exePath}' for resource update");

        bool ok = false;
        try
        {
            // Remove the existing entries of these types first, so no stale icon or version string survives.
            foreach (var (type, name, lang) in old)
                UpdateResourceW(h, type, name, lang, null, 0);

            if (icon != null)
            {
                for (int i = 0; i < icon.Count; i++)
                    Update(h, RT_ICON, (IntPtr)(i + 1), icon[i].Data);
                Update(h, RT_GROUP_ICON, (IntPtr)1, BuildGroupIcon(icon));
            }

            if (version != null)
                Update(h, RT_VERSION, (IntPtr)1, BuildVersionBlob(version));

            ok = true;
        }
        finally
        {
            EndUpdateResourceW(h, !ok);
            // String names were copied to unmanaged memory; a name with several languages appears several times.
            foreach (var ptr in old.Select(e => e.Name).Where(n => ((long)n >> 16) != 0).Distinct())
                Marshal.FreeHGlobal(ptr);
        }
    }

    // ── resource enumeration ─────────────────────────────────────────────────

    private static List<(IntPtr Type, IntPtr Name, ushort Lang)> Enumerate(string file, List<IntPtr> types)
    {
        var result = new List<(IntPtr, IntPtr, ushort)>();
        IntPtr module = LoadLibraryExW(file, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot read resources of '{file}'");

        try
        {
            foreach (var type in types)
            {
                var names = new List<IntPtr>();
                // Fails with ERROR_RESOURCE_TYPE_NOT_FOUND when the type has no entries: that just means "nothing to remove".
                EnumResourceNamesW(module, type, (m, tt, n, p) => { names.Add(Copy(n)); return true; }, IntPtr.Zero);

                foreach (var name in names)
                {
                    var resType = type;
                    var resName = name;
                    var found = false;
                    EnumResourceLanguagesW(module, resType, resName, (m, tt, nn, lang, p) =>
                    {
                        result.Add((resType, resName, lang));
                        found = true;
                        return true;
                    }, IntPtr.Zero);

                    // A string name with no languages would leak its copy: free it now (names with languages are freed by Patch).
                    if (!found && ((long)resName >> 16) != 0) Marshal.FreeHGlobal(resName);
                }
            }
        }
        finally
        {
            FreeLibrary(module);
        }
        return result;
    }

    // Integer ids are small values; string names are pointers that are only valid during the callback, so copy them.
    private static IntPtr Copy(IntPtr name)
        => ((long)name >> 16) == 0 ? name : Marshal.StringToHGlobalUni(Marshal.PtrToStringUni(name));

    private static void Update(IntPtr h, IntPtr type, IntPtr name, byte[] data)
    {
        if (!UpdateResourceW(h, type, name, Lang, data, (uint)data.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateResource failed");
    }

    // ── icon parsing ─────────────────────────────────────────────────────────

    private static List<IconImage> ParseIco(byte[] f, string path)
    {
        if (f.Length < 6 || BitConverter.ToUInt16(f, 0) != 0 || BitConverter.ToUInt16(f, 2) != 1)
            throw new ArgumentException($"Not a valid .ico file: {path}");

        int count = BitConverter.ToUInt16(f, 4);
        if (count == 0 || f.Length < 6 + count * 16) throw new ArgumentException($"Empty or truncated .ico file: {path}");

        var list = new List<IconImage>();
        for (int i = 0; i < count; i++)
        {
            int e = 6 + i * 16;
            int size = BitConverter.ToInt32(f, e + 8);
            int offset = BitConverter.ToInt32(f, e + 12);
            if (size <= 0 || offset < 0 || (long)offset + size > f.Length)
                throw new ArgumentException($"Corrupt image entry {i} in .ico file: {path}");

            list.Add(new IconImage
            {
                Width = f[e],
                Height = f[e + 1],
                ColorCount = f[e + 2],
                Planes = BitConverter.ToUInt16(f, e + 4),
                BitCount = BitConverter.ToUInt16(f, e + 6),
                Data = f.AsSpan(offset, size).ToArray()
            });
        }
        return list;
    }

    private static IconImage FromPng(byte[] f, string path)
    {
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (f.Length < 24 || !f.AsSpan(0, 8).SequenceEqual(sig))
            throw new ArgumentException($"Not a valid .png file: {path}");

        int w = ReadBe(f, 16), h = ReadBe(f, 20);
        if (w < 1 || h < 1 || w > 256 || h > 256)
            throw new ArgumentException($"PNG icon must be at most 256x256 (got {w}x{h}): {path}");

        return new IconImage
        {
            Width = (byte)(w == 256 ? 0 : w),
            Height = (byte)(h == 256 ? 0 : h),
            Planes = 1,
            BitCount = 32,
            Data = f
        };
    }

    private static int ReadBe(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    // GRPICONDIR: 6-byte header + 14 bytes per image (like ICONDIRENTRY, but the offset is replaced by the RT_ICON id).
    private static byte[] BuildGroupIcon(List<IconImage> images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            var im = images[i];
            w.Write(im.Width);
            w.Write(im.Height);
            w.Write(im.ColorCount);
            w.Write((byte)0);
            w.Write(im.Planes == 0 ? (ushort)1 : im.Planes);   // some icon tools write 0; the group entry always says 1
            w.Write(im.BitCount);
            w.Write((uint)im.Data.Length);
            w.Write((ushort)(i + 1));
        }
        return ms.ToArray();
    }

    // ── version resource ─────────────────────────────────────────────────────

    private static byte[] BuildVersionBlob(VersionInfo v)
    {
        var (ms, ls) = ParseVersion(v.Version);

        using var ms0 = new MemoryStream();
        using var w = new BinaryWriter(ms0, Encoding.Unicode);

        Node(w, "VS_VERSION_INFO", valueLength: 52, text: false, value: () =>
        {
            w.Write(0xFEEF04BDu);   // dwSignature
            w.Write(0x00010000u);   // dwStrucVersion
            w.Write(ms); w.Write(ls);   // file version
            w.Write(ms); w.Write(ls);   // product version
            w.Write(0x3Fu);         // dwFileFlagsMask
            w.Write(0u);            // dwFileFlags
            w.Write(0x00040004u);   // VOS_NT_WINDOWS32
            w.Write(1u);            // VFT_APP
            w.Write(0u);            // dwFileSubtype
            w.Write(0u); w.Write(0u);   // dwFileDate
        }, children: () =>
        {
            Node(w, "StringFileInfo", 0, true, null, () =>
            {
                Node(w, "040904b0", 0, true, null, () =>
                {
                    Str(w, "CompanyName", v.CompanyName);
                    Str(w, "FileDescription", v.FileDescription);
                    Str(w, "FileVersion", v.Version);
                    Str(w, "InternalName", v.InternalName);
                    Str(w, "LegalCopyright", v.LegalCopyright);
                    Str(w, "OriginalFilename", v.OriginalFilename);
                    Str(w, "ProductName", v.ProductName);
                    Str(w, "ProductVersion", v.Version);
                });
            });
            Node(w, "VarFileInfo", 0, true, null, () =>
            {
                Node(w, "Translation", 4, false, () => w.Write(0x04B00409u), null);
            });
        });

        return ms0.ToArray();
    }

    // One version-resource node: wLength, wValueLength, wType, key, pad, value, pad, children.
    // Nodes start on a 4-byte boundary. wLength covers the node and its children but not the padding after the node.
    private static void Node(BinaryWriter w, string key, int valueLength, bool text, Action? value, Action? children)
    {
        Align(w);
        long start = w.BaseStream.Position;
        w.Write((ushort)0);                 // wLength, patched below
        w.Write((ushort)valueLength);
        w.Write((ushort)(text ? 1 : 0));
        WriteZ(w, key);
        Align(w);
        value?.Invoke();
        children?.Invoke();

        long end = w.BaseStream.Position;
        w.BaseStream.Position = start;
        w.Write((ushort)(end - start));
        w.BaseStream.Position = end;
    }

    // String entry: wValueLength counts UTF-16 characters including the terminating zero.
    private static void Str(BinaryWriter w, string key, string value)
    {
        Align(w);
        long start = w.BaseStream.Position;
        w.Write((ushort)0);
        w.Write((ushort)(value.Length + 1));
        w.Write((ushort)1);
        WriteZ(w, key);
        Align(w);
        WriteZ(w, value);

        long end = w.BaseStream.Position;
        w.BaseStream.Position = start;
        w.Write((ushort)(end - start));
        w.BaseStream.Position = end;
    }

    private static void WriteZ(BinaryWriter w, string s)
    {
        foreach (char c in s) w.Write((ushort)c);
        w.Write((ushort)0);
    }

    private static void Align(BinaryWriter w)
    {
        while (w.BaseStream.Position % 4 != 0) w.Write((byte)0);
    }

    // "1.2.3.4" -> (0x00010002, 0x00030004). Missing parts are 0; extra text after a part (e.g. "1.2.3-beta") is ignored.
    private static (uint Ms, uint Ls) ParseVersion(string text)
    {
        var parts = new ushort[4];
        var tokens = text.Split('.');
        for (int i = 0; i < 4 && i < tokens.Length; i++)
        {
            string digits = new string(tokens[i].TakeWhile(char.IsDigit).ToArray());
            if (digits.Length > 0 && ushort.TryParse(digits, out ushort p)) parts[i] = p;
        }
        return (((uint)parts[0] << 16) | parts[1], ((uint)parts[2] << 16) | parts[3]);
    }
}