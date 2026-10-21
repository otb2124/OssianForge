using System.Text.Json;

namespace OssianForge.Cooker;

/// <summary>
/// Optional cook.json (default location: &lt;content&gt;/cook.json). Everything is relative to the Content folder.
/// <code>
/// {
///   "manifest": "ConfigFiles/Core/resources.json",
///   "raw":     ["ConfigFiles/**"],        // copied verbatim (glob: * ** ?)
///   "scripts": ["ScriptFiles/Packs"],     // every .cs below is compiled to its own assembly
///   "exclude": ["**/*.import.json"]
/// }
/// </code>
/// </summary>
internal sealed class CookConfig
{
    public string? Manifest { get; set; }
    public List<string> Raw { get; set; } = new() { "ConfigFiles/**" };
    public List<string> Scripts { get; set; } = new() { "ScriptFiles/Packs" };
    public List<string> Exclude { get; set; } = new() { "**/*.import.json" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static CookConfig Load(CookOptions o, string contentDir)
    {
        string? path = o.ConfigPath;
        if (path == null)
        {
            string candidate = Path.Combine(contentDir, "cook.json");
            if (File.Exists(candidate)) path = candidate;
        }

        var cfg = new CookConfig();
        if (path != null)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"Config not found: {path}");
            cfg = JsonSerializer.Deserialize<CookConfig>(File.ReadAllText(path), JsonOpts) ?? new CookConfig();
            Log.Debug($"Using config {path}");
        }

        // Cook.ini / command line win over cook.json
        if (o.Raw != null) cfg.Raw = o.Raw;
        if (o.Scripts != null) cfg.Scripts = o.Scripts;
        if (o.Exclude != null) cfg.Exclude = o.Exclude;

        cfg.Manifest = o.Manifest ?? cfg.Manifest ?? "ConfigFiles/Core/resources.json";
        return cfg;
    }
}