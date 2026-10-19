using OssianForge.Content;
using System.Text.RegularExpressions;

namespace OssianForge.Cooker;

internal sealed class AssetJob
{
    /// <summary>Content-relative, '/'-separated, original case. Also the pack key.</summary>
    public string Path = "";
    public AssetType Type;
    public Hints Hints = new();
    public string Origin = "";
}

/// <summary>Decides which files get cooked and how: from resources.json plus the raw/scripts globs in cook.json.</summary>
internal static class CookPlan
{
    // resources.json "type" -> asset type. Types not listed here are logical resources (no file of their own).
    private static readonly Dictionary<string, AssetType> TypeMap = new(StringComparer.Ordinal)
    {
        ["meshfile"] = AssetType.Mesh,
        ["animationfile"] = AssetType.Animation,
        ["texturefile"] = AssetType.Texture,
        ["soundfile"] = AssetType.Sound,
        ["shaderfile"] = AssetType.Shader,
        ["scriptfile"] = AssetType.Script,
    };

    public static List<AssetJob> Build(string contentDir, CookConfig cfg, out int errors)
    {
        errors = 0;
        var jobs = new Dictionary<string, AssetJob>();
        var exclude = cfg.Exclude.Select(Glob).ToList();

        bool Excluded(string rel) => exclude.Any(rx => rx.IsMatch(rel));

        void Add(string rel, AssetType type, string origin, Hints? hints = null)
        {
            rel = PathUtil.NormalizeSlashes(rel);
            if (Excluded(rel)) { Log.Debug($"excluded: {rel}"); return; }

            string key = PathUtil.Normalize(rel);
            if (jobs.TryGetValue(key, out var existing))
            {
                if (existing.Type != type)
                    Log.Warn($"'{rel}' is used as both {existing.Type} ({existing.Origin}) and {type} ({origin}); keeping {existing.Type}");
                return;
            }
            jobs[key] = new AssetJob { Path = rel, Type = type, Origin = origin, Hints = hints ?? new Hints() };
        }

        // 1. resources.json
        string manifestPath = Path.Combine(contentDir, cfg.Manifest!);
        if (!File.Exists(manifestPath))
        {
            Log.Error(cfg.Manifest!, "manifest not found");
            errors++;
        }
        else
        {
            var records = Manifest.Read(manifestPath);
            var texPathById = records
                .Where(r => r.Type == "texturefile" && r.Data.Count > 0)
                .ToDictionary(r => r.Id, r => r.Data[0], StringComparer.Ordinal);
            var texPaths = new HashSet<string>(texPathById.Values.Select(PathUtil.Normalize));

            string? ResolveTexture(string idOrPath)
            {
                if (texPathById.TryGetValue(idOrPath, out var p)) return p;
                return texPaths.Contains(PathUtil.Normalize(idOrPath)) ? idOrPath : null;
            }

            // Hints: how a texture is consumed decides how it must be cooked (see CubemapTextureResource / HeightmapMeshResource).
            var hints = new Dictionary<string, Hints>();
            foreach (var r in records)
            {
                switch (r.Type)
                {
                    case "cubemaptexture":
                        foreach (var face in r.Data)
                            if (ResolveTexture(face) is { } facePath)
                                hints[PathUtil.Normalize(facePath)] = new Hints(FlipY: false, Mips: false);
                        break;
                    case "terrainmesh":
                    case "terraincollider":
                        if (r.Data.Count > 0 && ResolveTexture(r.Data[0]) is { } hm)
                            hints.TryAdd(PathUtil.Normalize(hm), new Hints(Mips: false));
                        break;
                }
            }

            foreach (var r in records)
            {
                if (r.Data.Count == 0) continue;

                if (TypeMap.TryGetValue(r.Type, out var type))
                {
                    hints.TryGetValue(PathUtil.Normalize(r.Data[0]), out var h);
                    Add(r.Data[0], type, r.Id, h);
                }
                else if (r.Type.StartsWith("configfile.", StringComparison.Ordinal))
                    Add(r.Data[0], AssetType.Raw, r.Id);
                // anything else (mesh, texture, shader, collider, font, ...) is a logical resource built at runtime from the files above
            }
        }

        // 2. raw globs
        var rawGlobs = cfg.Raw.Select(Glob).ToList();
        // 3. script folders
        var scriptFolders = cfg.Scripts.Select(PathUtil.NormalizeSlashes).ToList();

        foreach (var file in Directory.EnumerateFiles(contentDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(contentDir, file).Replace('\\', '/');

            if (rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && scriptFolders.Any(f => rel.StartsWith(f.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
            {
                Add(rel, AssetType.Script, "scripts-folder");
                continue;
            }
            if (rawGlobs.Any(rx => rx.IsMatch(rel)))
                Add(rel, AssetType.Raw, "raw-glob");
        }

        return jobs.Values.OrderBy(j => PathUtil.Normalize(j.Path), StringComparer.Ordinal).ToList();
    }

    /// <summary>Glob -> regex. Supports "**" (any depth), "*" (within a folder) and "?".</summary>
    public static Regex Glob(string pattern)
    {
        string p = PathUtil.NormalizeSlashes(pattern);
        var sb = new System.Text.StringBuilder("^");
        for (int i = 0; i < p.Length; i++)
        {
            char c = p[i];
            if (c == '*')
            {
                if (i + 1 < p.Length && p[i + 1] == '*')
                {
                    i++;
                    if (i + 1 < p.Length && p[i + 1] == '/') { i++; sb.Append("(?:.*/)?"); }
                    else sb.Append(".*");
                }
                else sb.Append("[^/]*");
            }
            else if (c == '?') sb.Append("[^/]");
            else sb.Append(Regex.Escape(c.ToString()));
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
