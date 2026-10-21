namespace OssianForge.Cooker;

internal sealed class CookOptions
{
    public string ContentDir = "Content";
    public string? ConfigPath;
    public string? Manifest;                 // content-relative path of resources.json
    public string Output = "Content.pak";
    public string? CacheDir;
    public bool NoCache;
    public string? EngineDir;                // folder with the built engine (OssianForge.dll + deps), needed for scripts
    public int Jobs = Math.Max(1, Environment.ProcessorCount);
    public bool Compress = true;
    public bool AllowErrors;                 // write the pack even if some assets failed
    public bool Json;
    public bool Verbose;

    // Globs from Cook.ini. null = not set, fall back to cook.json / built-in defaults.
    public List<string>? Raw;
    public List<string>? Scripts;
    public List<string>? Exclude;

    public string? IniPath;                  // Cook.ini that was applied, if any
    public List<string> IniWarnings = new();

    public List<string> Positional = new();

    public static CookOptions Parse(string[] args, int start)
    {
        var o = new CookOptions();

        // Cook.ini is applied first so command-line options override it.
        string? explicitIni = null;
        for (int i = start; i < args.Length; i++)
            if (args[i] == "--ini")
                explicitIni = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value for --ini");
        CookIni.Apply(o, explicitIni);

        for (int i = start; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Missing value for {a}");

            switch (a)
            {
                case "--ini": Next(); break;   // already handled above
                case "--content": o.ContentDir = Next(); break;
                case "--config": o.ConfigPath = Next(); break;
                case "--manifest": o.Manifest = Next(); break;
                case "--out": case "-o": o.Output = Next(); break;
                case "--cache": o.CacheDir = Next(); break;
                case "--no-cache": o.NoCache = true; break;
                case "--engine-dir": o.EngineDir = Next(); break;
                case "--jobs": case "-j": o.Jobs = Math.Max(1, int.Parse(Next())); break;
                case "--no-compress": o.Compress = false; break;
                case "--allow-errors": o.AllowErrors = true; break;
                case "--json": o.Json = true; break;
                case "--verbose": case "-v": o.Verbose = true; break;
                default:
                    if (a.StartsWith('-')) throw new ArgumentException($"Unknown option: {a}");
                    o.Positional.Add(a);
                    break;
            }
        }

        // Deployed layout <engine output>\OssianCooker\: the engine build is the parent folder.
        o.EngineDir ??= GuessEngineDir();
        return o;
    }

    private static string? GuessEngineDir()
    {
        string? parent = Directory.GetParent(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
        return parent != null && File.Exists(Path.Combine(parent, "OssianForge.dll")) ? parent : null;
    }
}