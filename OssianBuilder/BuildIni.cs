namespace OssianForge.Builder;

/// <summary>
/// Reads Build.ini into <see cref="BuildOptions"/>. Precedence: command line > Build.ini > built-in defaults.
/// Same conventions as OssianCooker's Cook.ini: relative path keys are resolved from the folder containing
/// Build.ini, searching parent folders if the target is not found there. Without --ini the nearest Build.ini is
/// searched upwards from the working directory, then from the exe directory.
/// </summary>
internal static class BuildIni
{
    public const string FileName = "Build.ini";

    public static void Apply(BuildOptions o, string? explicitPath)
    {
        string? path = explicitPath != null ? Path.GetFullPath(explicitPath) : Find();
        if (path == null) return;
        if (!File.Exists(path)) throw new ArgumentException($"Build.ini not found: {path}");

        string baseDir = Path.GetDirectoryName(path)!;
        string section = "";

        foreach (var rawLine in File.ReadAllText(path).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0) continue;

            string key = line[..eq].Trim();
            string value = ParseValue(line[(eq + 1)..]);

            switch ($"{section}.{key}".ToLowerInvariant())
            {
                case "app.name": o.AppName = value; break;
                case "app.exename": o.ExeName = value; break;
                case "app.icon": o.Icon = value.Length > 0 ? Abs(baseDir, value) : null; break;
                case "app.version": if (value.Length > 0) o.Version = value; break;
                case "app.company": o.Company = value; break;
                case "app.copyright": o.Copyright = value; break;
                case "app.description": o.Description = value; break;
                case "app.console": o.ConsoleWindow = Bool(path, key, value); break;

                case "content.path": if (value.Length > 0) o.ContentDir = Abs(baseDir, value); break;
                case "content.pak": if (value.Length > 0) o.PakName = value; break;
                case "content.resourcesconfigpath": if (value.Length > 0) o.Manifest = value; break;
                case "content.tree": o.Tree = value; break;
                case "content.cookini": o.CookIni = value.Length > 0 ? Abs(baseDir, value) : null; break;
                case "content.prebuiltpak": o.PrebuiltPak = value.Length > 0 ? Abs(baseDir, value) : null; break;
                case "content.allowcookerrors": o.AllowCookErrors = Bool(path, key, value); break;

                case "build.template": o.TemplateDir = value.Length > 0 ? Abs(baseDir, value) : null; break;
                case "build.output": if (value.Length > 0) o.OutputDir = Abs(baseDir, value, isOutputFile: true); break;
                case "build.cooker": o.CookerPath = value.Length > 0 ? Abs(baseDir, value) : null; break;
                case "build.zip": o.Zip = Bool(path, key, value); break;
                case "build.exclude": o.Exclude = List(value); break;
                default:
                    o.IniWarnings.Add($"{path}: ignoring unknown key '{section}.{key}'");
                    break;
            }
        }

        o.IniPath = path;
    }

    private static string? Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    // Relative paths: against the Build.ini folder; if the target is not there, against the nearest parent folder
    // where it exists (for the output folder: where its parent exists, so a new "dist" lands next to Build.ini).
    private static string Abs(string baseDir, string p, bool isOutputFile = false)
    {
        if (Path.IsPathRooted(p)) return Path.GetFullPath(p);

        for (var dir = new DirectoryInfo(baseDir); dir != null; dir = dir.Parent)
        {
            string full = Path.GetFullPath(p, dir.FullName);
            string probe = isOutputFile ? Path.GetDirectoryName(full) ?? full : full;
            if (Directory.Exists(probe) || File.Exists(probe)) return full;
        }
        return Path.GetFullPath(p, baseDir);
    }

    private static bool Bool(string file, string key, string v) => v.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "on" => true,
        "false" or "0" or "no" or "off" => false,
        _ => throw new ArgumentException($"{file}: '{key}' must be true or false, got '{v}'")
    };

    // Comma-separated. An empty value gives an empty list.
    public static List<string> List(string v)
        => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // Quoted value: everything between the quotes. Otherwise cut at an inline " ;" or " #" comment.
    private static string ParseValue(string raw)
    {
        string v = raw.Trim();
        if (v.Length >= 2 && v[0] == '"')
        {
            int close = v.IndexOf('"', 1);
            if (close > 0) return v[1..close];
        }

        for (int i = 0; i < v.Length; i++)
            if ((v[i] == ';' || v[i] == '#') && (i == 0 || char.IsWhiteSpace(v[i - 1])))
                return v[..i].TrimEnd();

        return v;
    }
}