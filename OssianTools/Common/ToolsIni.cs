using OssianForge.Tools.Build;
using OssianForge.Tools.Cook;

namespace OssianForge.Tools;

/// <summary>
/// Reads Tools.ini into the cook and/or build options. Precedence: command line > Tools.ini > cook.json > built-in defaults.
/// Keys that both commands need (Content.Path, Content.ResourcesConfigPath, Engine.Dir, Cook.AllowErrors) exist once and
/// feed both option objects. Without --ini the nearest Tools.ini is searched upwards from the working directory,
/// then from the exe directory.
/// </summary>
internal static class ToolsIni
{
    public const string FileName = "Tools.ini";

    /// <param name="c">Cook options, or null when the cook is not part of this run.</param>
    /// <param name="b">Build options, or null when this is a cook-only run.</param>
    public static void Apply(CookOptions? c, BuildOptions? b, string? explicitPath)
    {
        string? path = explicitPath != null ? Path.GetFullPath(explicitPath) : IniFile.Find(FileName);
        if (path == null) return;
        if (!File.Exists(path)) throw new ArgumentException($"{FileName} not found: {path}");
        string dir = Path.GetDirectoryName(path)!;
        var warnings = (b?.IniWarnings ?? c?.IniWarnings)!;
        string Abs(string v, bool outFile = false) => IniFile.Abs(dir, v, outFile);

        foreach (var (section, key, value) in IniFile.Read(path))
        {
            bool has = value.Length > 0;
            switch ($"{section}.{key}".ToLowerInvariant())
            {
                // ---- shared ----
                case "engine.dir":
                    if (has)
                    {
                        if (c != null) c.EngineDir = Abs(value);
                        if (b != null) b.TemplateDir = Abs(value);
                    }
                    break;
                case "content.path":
                    if (has)
                    {
                        if (c != null) c.ContentDir = Abs(value);
                        if (b != null) b.ContentDir = Abs(value);
                    }
                    break;
                case "content.resourcesconfigpath":
                    if (has)
                    {
                        if (c != null) c.Manifest = value;
                        if (b != null) b.Manifest = value;
                    }
                    break;
                case "cook.allowerrors":
                    {
                        bool v = IniFile.Bool(path, key, value);
                        if (c != null) c.AllowErrors = v;
                        if (b != null) b.AllowCookErrors = v;
                    }
                    break;

                // ---- cook ----
                case "cook.outputpath": if (c != null && has) c.Output = Abs(value, true); break;
                case "cook.cachedir": if (c != null && has) c.CacheDir = Abs(value); break;
                case "cook.nocache": { bool v = IniFile.Bool(path, key, value); if (c != null) c.NoCache = v; } break;
                case "cook.compress": { bool v = IniFile.Bool(path, key, value); if (c != null) c.Compress = v; } break;
                case "cook.jobs":
                    if (!int.TryParse(value, out int jobs) || jobs < 0)
                        throw new ArgumentException($"{path}: Cook.Jobs must be a number >= 0 (0 = CPU count), got '{value}'");
                    if (c != null && jobs > 0) c.Jobs = jobs;
                    break;
                case "cook.raw": if (c != null) c.Raw = IniFile.List(value); break;
                case "cook.scripts": if (c != null) c.Scripts = IniFile.List(value); break;
                case "cook.exclude": if (c != null) c.Exclude = IniFile.List(value); break;

                // ---- build ----
                case "app.name": if (b != null) b.AppName = value; break;
                case "app.exename": if (b != null) b.ExeName = value; break;
                case "app.icon": if (b != null) b.Icon = has ? Abs(value) : null; break;
                case "app.version": if (b != null && has) b.Version = value; break;
                case "app.company": if (b != null) b.Company = value; break;
                case "app.copyright": if (b != null) b.Copyright = value; break;
                case "app.description": if (b != null) b.Description = value; break;
                case "app.console": { bool v = IniFile.Bool(path, key, value); if (b != null) b.ConsoleWindow = v; } break;
                case "content.pak": if (b != null && has) b.PakName = value; break;
                case "content.treeconfigpath": if (b != null) b.TreeConfigPath = value; break;
                case "content.prebuiltpak": if (b != null) b.PrebuiltPak = has ? Abs(value) : null; break;
                case "build.output": if (b != null && has) b.OutputDir = Abs(value, true); break;
                case "build.zip": { bool v = IniFile.Bool(path, key, value); if (b != null) b.Zip = v; } break;
                case "build.exclude": if (b != null) b.Exclude = IniFile.List(value); break;
                case "redist.include": { bool v = IniFile.Bool(path, key, value); if (b != null) b.RedistInclude = v; } break;
                case "redist.path": if (b != null && has) b.RedistPath = Abs(value); break;
                case "redist.folder": if (b != null && has) b.RedistFolder = value; break;

                default:
                    warnings.Add($"{path}: ignoring unknown key '{section}.{key}'");
                    break;
            }
        }
        if (c != null) c.IniPath = path;
        if (b != null) b.IniPath = path;
    }
}
