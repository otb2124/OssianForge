using System.IO.Hashing;
using System.Text;

namespace OssianForge.Content;

/// <summary>
/// Logical content paths are relative to the Content folder, use '/' and are compared case-insensitively
/// (so a pack built on Windows behaves the same on Linux).
/// </summary>
public static class PathUtil
{
    /// <summary>Forward slashes, no leading "./" or "/", no duplicate slashes. Case is preserved.</summary>
    public static string NormalizeSlashes(string path)
    {
        string p = path.Replace('\\', '/');
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
        p = p.TrimStart('/');
        while (p.Contains("//", StringComparison.Ordinal)) p = p.Replace("//", "/");
        return p;
    }

    /// <summary>Lookup key: <see cref="NormalizeSlashes"/> + lower-case invariant.</summary>
    public static string Normalize(string path) => NormalizeSlashes(path).ToLowerInvariant();

    /// <summary>64-bit hash of an already-normalized key.</summary>
    public static ulong Hash(string normalizedKey) => XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(normalizedKey));
}
