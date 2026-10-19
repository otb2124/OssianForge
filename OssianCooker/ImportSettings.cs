using System.Text.Json;

namespace OssianForge.Cooker;

/// <summary>Context-derived defaults for a texture (e.g. cubemap faces must not be flipped or mipmapped).</summary>
internal sealed record Hints(bool? FlipY = null, bool? Mips = null)
{
    public string Tag => $"flip={FlipY?.ToString() ?? "-"};mips={Mips?.ToString() ?? "-"}";
}

/// <summary>
/// Optional per-asset sidecar "&lt;file&gt;.import.json", e.g. "remy_jump.fbx.import.json":
/// { "clip": "jump", "unitScale": 0.01 }   or   { "flipY": false, "mips": false, "srgb": true }
/// Sidecar values win over hints derived from the manifest.
/// </summary>
internal sealed class ImportSettings
{
    // meshes + animations
    public float? UnitScale { get; set; }
    /// <summary>Animations: clip to keep. Default = file name stem (current engine behavior). "*" keeps every clip.</summary>
    public string? Clip { get; set; }
    // textures
    public bool? FlipY { get; set; }
    public bool? Mips { get; set; }
    public bool? Srgb { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Loads the sidecar (if any), applies hints for unset fields. <paramref name="cacheTag"/> feeds the cache key.</summary>
    public static ImportSettings Resolve(string sourceFullPath, Hints hints, out string cacheTag)
    {
        var s = new ImportSettings();
        string sidecar = sourceFullPath + ".import.json";
        string sidecarText = "";
        if (File.Exists(sidecar))
        {
            sidecarText = File.ReadAllText(sidecar);
            s = JsonSerializer.Deserialize<ImportSettings>(sidecarText, JsonOpts) ?? new ImportSettings();
        }
        s.FlipY ??= hints.FlipY;
        s.Mips ??= hints.Mips;
        cacheTag = hints.Tag + "|" + sidecarText;
        return s;
    }
}
