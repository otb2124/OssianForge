using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OssianForge.Content;
using System.IO.Hashing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace OssianForge.Cooker.Importers;

/// <summary>
/// Compiles one .cs script to a DLL (same per-file model as ScriptFile.Load) so the shipped game needs no Roslyn.
/// References = the .NET runtime assemblies + every managed DLL in --engine-dir (the built engine and its dependencies).
/// </summary>
internal sealed class ScriptImporter : IImporter
{
    private readonly string? _engineDir;
    private readonly Lazy<(List<MetadataReference> Refs, string Fingerprint)> _refs;

    public ScriptImporter(string? engineDir)
    {
        _engineDir = engineDir;
        _refs = new Lazy<(List<MetadataReference>, string)>(BuildReferences);
    }

    public AssetType Type => AssetType.Script;
    public int Version => 1;

    public string Fingerprint(ImportContext ctx) => _refs.Value.Fingerprint;

    public byte[] Import(ImportContext ctx)
    {
        string source = File.ReadAllText(ctx.FullPath);

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(ctx.RelPath),
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source, path: ctx.RelPath) },
            references: _refs.Value.Refs,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                deterministic: true));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            string errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new Exception($"Script compile error:\n{errors}");
        }
        return ms.ToArray();
    }

    private (List<MetadataReference>, string) BuildReferences()
    {
        if (string.IsNullOrEmpty(_engineDir) || !Directory.Exists(_engineDir))
            throw new InvalidOperationException(
                "Scripts need --engine-dir <folder containing the built OssianForge.dll and its dependencies>.");

        var refs = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fp = new XxHash128();
        fp.Append(Encoding.UTF8.GetBytes(Environment.Version.ToString()));

        void AddDll(string path)
        {
            string simple = Path.GetFileNameWithoutExtension(path);
            if (simple.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) || !seen.Add(simple)) return;
            try { AssemblyName.GetAssemblyName(path); }
            catch (BadImageFormatException) { return; }   // native library
            catch (FileLoadException) { return; }

            refs.Add(MetadataReference.CreateFromFile(path));
            var info = new FileInfo(path);
            fp.Append(Encoding.UTF8.GetBytes($"{info.Name}|{info.Length}|{info.LastWriteTimeUtc.Ticks};"));
        }

        foreach (var dll in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
            AddDll(dll);
        foreach (var dll in Directory.EnumerateFiles(_engineDir, "*.dll"))
            AddDll(dll);

        if (!seen.Contains("OssianForge"))
            Log.Warn($"OssianForge.dll not found in {_engineDir}; scripts that use engine types will not compile.");

        return (refs, Convert.ToHexString(fp.GetCurrentHash()));
    }
}
