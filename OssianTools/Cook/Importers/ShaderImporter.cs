using OssianForge.Content;
using System.Text;
using System.Text.RegularExpressions;

namespace OssianForge.Tools.Cook.Importers;

/// <summary>
/// Resolves <c>#include "file"</c> (relative to the including file) and stores the GLSL text.
/// Compile validation (glslang) is a later step; the GPU driver still compiles at load time.
/// </summary>
internal sealed class ShaderImporter : IImporter
{
    public AssetType Type => AssetType.Shader;
    public int Version => 1;

    private static readonly Regex IncludeRx =
        new(@"^[ \t]*#[ \t]*include[ \t]+""([^""]+)""[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

    public string Fingerprint(ImportContext ctx) => Resolve(ctx.FullPath, new Stack<string>());

    public byte[] Import(ImportContext ctx)
        => new UTF8Encoding(false).GetBytes(Resolve(ctx.FullPath, new Stack<string>()));

    private static string Resolve(string fullPath, Stack<string> stack)
    {
        string normalized = Path.GetFullPath(fullPath);
        if (stack.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            throw new Exception($"Circular #include: {string.Join(" -> ", stack.Reverse().Append(normalized))}");

        stack.Push(normalized);
        string text = File.ReadAllText(normalized);
        string result = IncludeRx.Replace(text, m =>
        {
            string inc = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(normalized)!, m.Groups[1].Value));
            if (!File.Exists(inc))
                throw new FileNotFoundException($"#include \"{m.Groups[1].Value}\" not found (from {Path.GetFileName(normalized)})");
            return Resolve(inc, stack);
        });
        stack.Pop();
        return result;
    }
}
