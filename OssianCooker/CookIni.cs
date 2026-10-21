namespace OssianForge.Cooker;

/// <summary>
/// Reads Cook.ini into <see cref="CookOptions"/>. Precedence: command line > Cook.ini > cook.json > built-in defaults.
/// Path keys are resolved relative to the folder containing Cook.ini (not the working directory),
/// so the cook behaves the same wherever the cooker is started from.
/// Without --ini, the nearest Cook.ini is searched upwards from the working directory, then from the exe directory.
/// </summary>
internal static class CookIni
{
    public const string FileName = "Cook.ini";

    public static void Apply(CookOptions o, string? explicitPath)
    {
        string? path = explicitPath != null ? Path.GetFullPath(explicitPath) : Find();
        if (path == null) return;
        if (!File.Exists(path)) throw new ArgumentException($"Cook.ini not found: {path}");

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
                case "cook.contentpath": if (value.Length > 0) o.ContentDir = Abs(baseDir, value); break;
                case "cook.outputpath": if (value.Length > 0) o.Output = Abs(baseDir, value); break;
                case "cook.resourcesconfigpath": if (value.Length > 0) o.Manifest = value; break;
                case "cook.enginedir": if (value.Length > 0) o.EngineDir = Abs(baseDir, value); break;
                case "cook.cachedir": if (value.Length > 0) o.CacheDir = Abs(baseDir, value); break;
                case "cook.nocache": o.NoCache = Bool(key, value); break;
                case "cook.compress": o.Compress = Bool(key, value); break;
                case "cook.allowerrors": o.AllowErrors = Bool(key, value); break;
                case "cook.jobs":
                    if (!int.TryParse(value, out int jobs) || jobs < 0)
                        throw new ArgumentException($"{path}: Cook.Jobs must be a number >= 0 (0 = CPU count), got '{value}'");
                    if (jobs > 0) o.Jobs = jobs;
                    break;
                case "cook.raw": o.Raw = List(value); break;
                case "cook.scripts": o.Scripts = List(value); break;
                case "cook.exclude": o.Exclude = List(value); break;
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

    private static string Abs(string baseDir, string p) => Path.GetFullPath(p, baseDir);

    private static bool Bool(string key, string v) => v.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "on" => true,
        "false" or "0" or "no" or "off" => false,
        _ => throw new ArgumentException($"Cook.ini: '{key}' must be true or false, got '{v}'")
    };

    // Comma-separated. An empty value gives an empty list (e.g. "no excludes").
    private static List<string> List(string v)
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

        for (int i = 1; i < v.Length; i++)
            if ((v[i] == ';' || v[i] == '#') && char.IsWhiteSpace(v[i - 1]))
                return v[..i].TrimEnd();

        return v;
    }
}