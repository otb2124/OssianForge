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

    public List<string> Positional = new();

    public static CookOptions Parse(string[] args, int start)
    {
        var o = new CookOptions();
        for (int i = start; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Missing value for {a}");

            switch (a)
            {
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
        return o;
    }
}
