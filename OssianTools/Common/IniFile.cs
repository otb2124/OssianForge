namespace OssianForge.Tools;

/// <summary>Ini parsing shared by every tool command (replaces the duplicated CookIni / BuildIni helpers).</summary>
internal static class IniFile
{
    /// <summary>Nearest <paramref name="fileName"/> searching upwards from the working directory, then from the exe directory.</summary>
    public static string? Find(string fileName)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    public static IEnumerable<(string Section, string Key, string Value)> Read(string path)
    {
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
            yield return (section, line[..eq].Trim(), ParseValue(line[(eq + 1)..]));
        }
    }

    // Relative paths: against the ini folder; if the target is not there, against the nearest parent folder
    // where it exists (for an output path: where its folder exists). This lets the same Tools.ini work from the
    // project folder and from <root>\bin\<cfg>\<tfm>\OssianTools\.
    public static string Abs(string baseDir, string p, bool isOutputFile = false)
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

    public static bool Bool(string file, string key, string v) => v.ToLowerInvariant() switch
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
