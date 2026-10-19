using OssianForge.Content;
using OssianForge.Content.Pack;
using OssianForge.Cooker.Importers;
using System.Diagnostics;

namespace OssianForge.Cooker;

internal static class CookRunner
{
    public static int Cook(CookOptions o)
    {
        var total = Stopwatch.StartNew();

        string contentDir = Path.GetFullPath(o.ContentDir);
        if (!Directory.Exists(contentDir))
        {
            Log.Error(o.ContentDir, "content folder not found");
            return 2;
        }

        var config = CookConfig.Load(o, contentDir);
        var jobs = CookPlan.Build(contentDir, config, out int errors);
        Log.Info($"Cooking {jobs.Count} asset(s) from {contentDir}");

        string? cacheDir = o.NoCache ? null : o.CacheDir ?? DefaultCacheDir(contentDir);
        var cache = new BuildCache(cacheDir);

        IImporter[] importerList =
        {
            new RawImporter(), new MeshImporter(), new AnimationImporter(), new TextureImporter(),
            new SoundImporter(), new ShaderImporter(), new ScriptImporter(o.EngineDir)
        };
        var importers = importerList.ToDictionary(i => i.Type);

        var results = new byte[jobs.Count][];
        long sourceBytes = 0;
        int hits = 0, done = 0;

        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = o.Jobs }, i =>
        {
            var job = jobs[i];
            var sw = Stopwatch.StartNew();
            try
            {
                var (data, hit, srcLen) = CookOne(job, contentDir, importers[job.Type], cache, o);
                results[i] = data;
                Interlocked.Add(ref sourceBytes, srcLen);
                if (hit) Interlocked.Increment(ref hits);
                Log.Asset(job.Path, job.Type.ToString(), hit ? "cached" : "cooked", data.Length, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                Log.Error(job.Path, ex.Message);
            }
            Log.Progress(Interlocked.Increment(ref done), jobs.Count);
        });

        if (errors > 0 && !o.AllowErrors)
        {
            Log.Error("", $"{errors} error(s); pack not written (use --allow-errors to write it anyway).");
            return 1;
        }

        long packBytes;
        string outPath = Path.GetFullPath(o.Output);
        using (var writer = new PackWriter(outPath, o.Compress))
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                if (results[i] == null) continue; // failed asset with --allow-errors
                writer.Add(jobs[i].Path, jobs[i].Type, results[i]);
            }
            writer.Complete();
            packBytes = new FileInfo(outPath).Length;
        }

        Log.Done(new Dictionary<string, object?>
        {
            ["pack"] = outPath,
            ["assets"] = jobs.Count - errors,
            ["errors"] = errors,
            ["cacheHits"] = hits,
            ["sourceBytes"] = sourceBytes,
            ["packBytes"] = packBytes,
            ["seconds"] = Math.Round(total.Elapsed.TotalSeconds, 2)
        });
        return errors > 0 ? 1 : 0;
    }

    private static (byte[] Data, bool CacheHit, long SourceLength) CookOne(
        AssetJob job, string contentDir, IImporter importer, BuildCache cache, CookOptions o)
    {
        string full = Path.Combine(contentDir, job.Path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"source file not found (referenced by '{job.Origin}')");

        byte[] source = File.ReadAllBytes(full);
        if (!importer.Cacheable)
            return (source, false, source.Length);

        var settings = ImportSettings.Resolve(full, job.Hints, out string settingsTag);
        var ctx = new ImportContext { FullPath = full, RelPath = job.Path, Settings = settings, Options = o };

        string key = BuildCache.MakeKey(importer.GetType().Name, importer.Version,
            PathUtil.Normalize(job.Path), settingsTag, importer.Fingerprint(ctx), source);

        if (cache.TryGet(key, out var cached))
            return (cached, true, source.Length);

        byte[] data = importer.Import(ctx);
        cache.Put(key, data);
        return (data, false, source.Length);
    }

    public static string DefaultCacheDir(string contentDir)
    {
        string parent = Path.GetDirectoryName(contentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                        ?? contentDir;
        return Path.Combine(parent, ".ossian", "cache");
    }
}
