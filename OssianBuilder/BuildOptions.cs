namespace OssianForge.Builder;

internal sealed class BuildOptions
{
    // [App]
    public string AppName = "";
    public string ExeName = "";              // without .exe; empty = derived from AppName
    public string? Icon;                     // absolute path of .ico / .png
    public string Version = "1.0.0.0";
    public string Company = "";
    public string Copyright = "";
    public string Description = "";          // empty = AppName
    public bool ConsoleWindow = true;

    // [Content]
    public string ContentDir = "Content";
    public string PakName = "Content.pak";
    public string Manifest = "ConfigFiles/Core/resources.json";
    public string Tree = "";
    public string? CookIni;
    public string? PrebuiltPak;
    public bool AllowCookErrors;

    // [Build]
    public string? TemplateDir;              // null = auto-detect
    public string OutputDir = "dist";
    public string? CookerPath;               // null = <template>\OssianCooker\OssianCooker.exe
    public bool Zip;
    public List<string> Exclude = new();     // added to Packager.DefaultExclude

    // command line only
    public bool Force;
    public bool DryRun;
    public bool Json;
    public bool Verbose;

    public string? IniPath;                  // Build.ini that was applied, if any
    public List<string> IniWarnings = new();
    public List<string> Positional = new();

    /// <summary>Folder that relative paths on the command line are resolved against.</summary>
    public static string Cwd => Directory.GetCurrentDirectory();

    public static BuildOptions Parse(string[] args, int start)
    {
        var o = new BuildOptions();

        // Build.ini is applied first so command-line options override it.
        string? explicitIni = null;
        for (int i = start; i < args.Length; i++)
            if (args[i] == "--ini")
                explicitIni = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value for --ini");
        BuildIni.Apply(o, explicitIni);

        for (int i = start; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Missing value for {a}");

            switch (a)
            {
                case "--ini": Next(); break;   // already handled above
                case "--name": o.AppName = Next(); break;
                case "--exe-name": o.ExeName = Next(); break;
                case "--icon": o.Icon = Path.GetFullPath(Next()); break;
                case "--version": o.Version = Next(); break;
                case "--company": o.Company = Next(); break;
                case "--copyright": o.Copyright = Next(); break;
                case "--description": o.Description = Next(); break;
                case "--no-console": o.ConsoleWindow = false; break;
                case "--console": o.ConsoleWindow = true; break;
                case "--content": o.ContentDir = Path.GetFullPath(Next()); break;
                case "--pak-name": o.PakName = Next(); break;
                case "--manifest": o.Manifest = Next(); break;
                case "--tree": o.Tree = Next(); break;
                case "--cook-ini": o.CookIni = Path.GetFullPath(Next()); break;
                case "--prebuilt-pak": o.PrebuiltPak = Path.GetFullPath(Next()); break;
                case "--allow-errors": o.AllowCookErrors = true; break;
                case "--template": o.TemplateDir = Path.GetFullPath(Next()); break;
                case "--out": case "-o": o.OutputDir = Path.GetFullPath(Next()); break;
                case "--cooker": o.CookerPath = Path.GetFullPath(Next()); break;
                case "--zip": o.Zip = true; break;
                case "--exclude": o.Exclude.AddRange(BuildIni.List(Next())); break;
                case "--force": o.Force = true; break;
                case "--dry-run": o.DryRun = true; break;
                case "--json": o.Json = true; break;
                case "--verbose": case "-v": o.Verbose = true; break;
                default:
                    if (a.StartsWith('-')) throw new ArgumentException($"Unknown option: {a}");
                    o.Positional.Add(a);
                    break;
            }
        }

        // A relative OutputDir / ContentDir that came from the defaults (no ini, no flag) is relative to the cwd.
        o.OutputDir = Path.GetFullPath(o.OutputDir);
        o.ContentDir = Path.GetFullPath(o.ContentDir);
        return o;
    }

    /// <summary>Exe file name without extension: explicit ExeName, else AppName without characters Windows dislikes.</summary>
    public string ResolveExeName()
    {
        string raw = ExeName.Length > 0 ? ExeName : AppName;
        if (raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) raw = raw[..^4];

        var bad = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
            if (!char.IsWhiteSpace(c) && Array.IndexOf(bad, c) < 0) sb.Append(c);

        string name = sb.ToString().Trim('.');
        if (name.Length == 0) throw new ArgumentException("App.Name / App.ExeName gives an empty exe file name.");
        return name;
    }
}