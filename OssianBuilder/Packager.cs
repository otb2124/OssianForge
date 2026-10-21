using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace OssianForge.Builder;

/// <summary>
/// Turns the already-built engine (the "template": OssianForge.exe + OssianForge.dll + deps) into a shippable game folder:
/// copy without the dev tools, cook the content into a pak, rename the exe and patch its icon/version info,
/// and write a pak-mode Ossian.ini. Nothing is compiled, and the template is never modified.
/// </summary>
internal static class Packager
{
    /// <summary>The engine's assembly name: its apphost is OssianForge.exe and it loads OssianForge.dll.</summary>
    private const string EngineName = "OssianForge";
    private const string IniName = "Ossian.ini";
    private const int StageCount = 6;

    // Never shipped. A leading "/" anchors the pattern at the engine folder's top level; others match at any depth.
    // Extra patterns come from Build.Exclude / --exclude.
    private static readonly string[] DefaultExclude =
    {
        "/OssianCooker", "/OssianBuilder",             // the dev tools
        "/Content", "/.ossian",                        // raw assets and the import cache (the pak replaces them)
        "*.pdb", "*.pak",                              // debug symbols, stray packs
        "/" + IniName, "/Cook.ini", "/Build.ini"       // dev-time settings; a fresh Ossian.ini is written
    };

    private sealed class Stats { public int Files; public long Bytes; public int Skipped; }

    // ── build ────────────────────────────────────────────────────────────────

    public static int Build(BuildOptions o)
    {
        var total = Stopwatch.StartNew();
        Log.Stage("prepare", 1, StageCount);

        // ---- resolve and validate everything before touching the disk ----
        string appName = o.AppName.Trim();
        if (appName.Length == 0) return Fail("App.Name is required (Build.ini [App] Name, or --name).");
        if (appName.Contains('"')) return Fail("App.Name must not contain double quotes.");
        if (o.Version.Contains('"')) return Fail("App.Version must not contain double quotes.");

        string exeName = o.ResolveExeName();
        if (exeName.Equals(EngineName + "Data", StringComparison.OrdinalIgnoreCase))
            return Fail($"'{exeName}' is not a usable exe name.");

        if (Path.GetFileName(o.PakName) != o.PakName || o.PakName.Length == 0)
            return Fail($"Content.Pak must be a plain file name, got '{o.PakName}'.");

        string? template = ResolveTemplate(o);
        if (template == null)
            return Fail($"Cannot find the built engine ({EngineName}.dll next to or above this tool). Pass --template <folder>.");
        if (ValidateTemplate(template) is { } templateError) return Fail(templateError);

        string? cooker = null;
        if (o.PrebuiltPak != null)
        {
            if (!File.Exists(o.PrebuiltPak)) return Fail($"Prebuilt pak not found: {o.PrebuiltPak}");
        }
        else
        {
            cooker = o.CookerPath ?? Path.Combine(template, "OssianCooker", "OssianCooker.exe");
            if (!File.Exists(cooker)) return Fail($"OssianCooker not found: {cooker} (use --cooker, or --prebuilt-pak to skip cooking).");
            if (!Directory.Exists(o.ContentDir)) return Fail($"Content folder not found: {o.ContentDir}");
            if (o.CookIni != null && !File.Exists(o.CookIni)) return Fail($"Cook.ini not found: {o.CookIni}");
        }

        List<PeEditor.IconImage>? icon = null;
        if (o.Icon != null)
        {
            if (!File.Exists(o.Icon)) return Fail($"Icon not found: {o.Icon}");
            try { icon = PeEditor.LoadIcon(o.Icon); }
            catch (ArgumentException ex) { return Fail(ex.Message); }
        }

        string outputDir = o.OutputDir;
        string gameDir = Path.Combine(outputDir, exeName);
        string tmpDir = Path.Combine(outputDir, "." + exeName + ".building");

        // The output must never overlap the template: we would copy a folder into itself, or delete the engine.
        if (IsInside(gameDir, template) || IsInside(template, outputDir) || SamePath(gameDir, template))
            return Fail($"Output '{gameDir}' overlaps the engine folder '{template}'. Choose an --out outside the build folder.");

        if (Directory.Exists(gameDir) && !o.Force && !IsEmpty(gameDir) && !LooksLikeBuild(gameDir))
            return Fail($"'{gameDir}' exists and does not look like a previous build. Use --force to replace it.");

        string zipPath = Path.Combine(outputDir, $"{exeName}-{o.Version}.zip");

        Log.Info($"App       : {appName}  ({exeName}.exe, v{o.Version})");
        Log.Info($"Template  : {template}");
        Log.Info(o.PrebuiltPak != null ? $"Content   : prebuilt pak {o.PrebuiltPak}" : $"Content   : cook {o.ContentDir}");
        Log.Info($"Output    : {gameDir}");
        if (o.IniPath != null) Log.Info($"Settings  : {o.IniPath}");
        foreach (var w in o.IniWarnings) Log.Warn(w);

        var exclude = new ExcludeSet(DefaultExclude.Concat(o.Exclude));

        if (o.DryRun)
        {
            var plan = new Stats();
            CopyTree(template, template, null, exclude, plan);
            Log.Done(new Dictionary<string, object?>
            {
                ["dryRun"] = true, ["game"] = gameDir, ["files"] = plan.Files,
                ["bytes"] = plan.Bytes, ["skipped"] = plan.Skipped, ["zip"] = o.Zip ? zipPath : null
            });
            return 0;
        }

        // ---- build into a temp folder; only a complete build replaces the previous one ----
        Directory.CreateDirectory(outputDir);
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        Directory.CreateDirectory(tmpDir);

        bool success = false;
        try
        {
            // 2. copy the engine
            Log.Stage("copy engine", 2, StageCount);
            var stats = new Stats();
            CopyTree(template, template, tmpDir, exclude, stats);
            Log.Info($"Copied {stats.Files} file(s), {Log.FormatSize(stats.Bytes)} ({stats.Skipped} entr{(stats.Skipped == 1 ? "y" : "ies")} left out).");

            string exePath = Path.Combine(tmpDir, exeName + ".exe");
            if (!exeName.Equals(EngineName, StringComparison.OrdinalIgnoreCase))
                File.Move(Path.Combine(tmpDir, EngineName + ".exe"), exePath);

            // 3. content
            string pakPath = Path.Combine(tmpDir, o.PakName);
            if (o.PrebuiltPak != null)
            {
                Log.Stage("copy prebuilt pak", 3, StageCount);
                File.Copy(o.PrebuiltPak, pakPath, true);
            }
            else
            {
                Log.Stage("cook content", 3, StageCount);
                int code = RunCooker(cooker!, o, template, pakPath);
                if (code != 0) return Fail($"OssianCooker failed (exit code {code}); build aborted.", 1);
                if (!File.Exists(pakPath)) return Fail("OssianCooker reported success but wrote no pack.", 1);
            }

            // 4. brand the exe
            Log.Stage("brand exe", 4, StageCount);
            if (!OperatingSystem.IsWindows())
            {
                Log.Warn("Not running on Windows: skipping icon/version/subsystem patching.");
            }
            else
            {
                try
                {
                    if (!o.ConsoleWindow) PeEditor.SetSubsystem(exePath, gui: true);
                    PeEditor.Patch(exePath, icon, new PeEditor.VersionInfo
                    {
                        Version = o.Version,
                        ProductName = appName,
                        FileDescription = o.Description.Length > 0 ? o.Description : appName,
                        CompanyName = o.Company,
                        LegalCopyright = o.Copyright,
                        OriginalFilename = exeName + ".exe",
                        InternalName = exeName
                    });
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidDataException)
                {
                    return Fail($"Patching {exeName}.exe failed: {ex.Message}", 1);
                }
            }

            // 5. runtime config
            Log.Stage("write config", 5, StageCount);
            File.WriteAllText(Path.Combine(tmpDir, IniName), BuildIni(o, appName), new UTF8Encoding(false));

            // 6. publish the result
            Log.Stage(o.Zip ? "finalize and zip" : "finalize", 6, StageCount);
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
            Directory.Move(tmpDir, gameDir);
            success = true;

            long zipBytes = 0;
            if (o.Zip)
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(gameDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
                zipBytes = new FileInfo(zipPath).Length;
            }

            Log.Done(new Dictionary<string, object?>
            {
                ["game"] = gameDir,
                ["exe"] = Path.Combine(gameDir, exeName + ".exe"),
                ["pak"] = Path.Combine(gameDir, o.PakName),
                ["pakBytes"] = new FileInfo(Path.Combine(gameDir, o.PakName)).Length,
                ["files"] = stats.Files + 2,   // + pak + Ossian.ini
                ["zip"] = o.Zip ? zipPath : null,
                ["zipBytes"] = zipBytes,
                ["seconds"] = Math.Round(total.Elapsed.TotalSeconds, 2)
            });
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(ex.Message, 1);
        }
        finally
        {
            if (!success)
            {
                try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); }
                catch (Exception ex) { Log.Warn($"Could not remove the temp folder {tmpDir}: {ex.Message}"); }
            }
        }
    }

    // ── clean ────────────────────────────────────────────────────────────────

    public static int Clean(BuildOptions o)
    {
        string exeName;
        try { exeName = o.ResolveExeName(); }
        catch (ArgumentException ex) { return Fail(ex.Message); }

        string gameDir = Path.Combine(o.OutputDir, exeName);
        string tmpDir = Path.Combine(o.OutputDir, "." + exeName + ".building");
        int removed = 0;

        if (Directory.Exists(tmpDir)) { Directory.Delete(tmpDir, true); removed++; }

        if (Directory.Exists(gameDir))
        {
            if (!o.Force && !IsEmpty(gameDir) && !LooksLikeBuild(gameDir))
                return Fail($"'{gameDir}' does not look like a build output. Use --force to delete it anyway.");
            Directory.Delete(gameDir, true);
            removed++;
        }

        Log.Info(removed > 0 ? $"Removed {removed} folder(s) under {o.OutputDir}" : "Nothing to clean.");
        return 0;
    }

    // ── template ─────────────────────────────────────────────────────────────

    // Deployed layout: <engine output>\OssianBuilder\OssianBuilder.exe, so the engine build is the parent folder.
    private static string? ResolveTemplate(BuildOptions o)
    {
        if (o.TemplateDir != null) return Path.GetFullPath(o.TemplateDir).TrimEnd(Path.DirectorySeparatorChar);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, EngineName + ".dll"))) return dir.FullName;
        return null;
    }

    private static string? ValidateTemplate(string template)
    {
        if (!Directory.Exists(template)) return $"Engine folder not found: {template}";

        // A single-file publish has no OssianForge.dll next to the exe, and its payload would not survive resource patching.
        if (!File.Exists(Path.Combine(template, EngineName + ".dll")))
            return $"{template} has no {EngineName}.dll. The template must be a normal multi-file build output, not a single-file publish.";
        if (!File.Exists(Path.Combine(template, EngineName + ".exe")))
            return $"{template} has no {EngineName}.exe.";
        if (!File.Exists(Path.Combine(template, EngineName + ".runtimeconfig.json")))
            return $"{template} has no {EngineName}.runtimeconfig.json (is this a build output?).";
        return null;
    }

    // Copies src into dst (dst == null: only counts, for --dry-run), skipping excluded files and folders.
    private static void CopyTree(string root, string src, string? dst, ExcludeSet exclude, Stats stats)
    {
        if (dst != null) Directory.CreateDirectory(dst);

        foreach (var file in Directory.EnumerateFiles(src))
        {
            string name = Path.GetFileName(file);
            if (exclude.IsExcluded(Path.GetRelativePath(root, file), name)) { stats.Skipped++; Log.Debug($"skip {Path.GetRelativePath(root, file)}"); continue; }

            if (dst != null) File.Copy(file, Path.Combine(dst, name), true);
            stats.Files++;
            stats.Bytes += new FileInfo(file).Length;
        }

        foreach (var dir in Directory.EnumerateDirectories(src))
        {
            string name = Path.GetFileName(dir);
            if (exclude.IsExcluded(Path.GetRelativePath(root, dir), name)) { stats.Skipped++; Log.Debug($"skip {Path.GetRelativePath(root, dir)}{Path.DirectorySeparatorChar}"); continue; }

            string target = dst == null ? null! : Path.Combine(dst, name);
            CopyTree(root, dir, dst == null ? null : target, exclude, stats);

            // A folder whose files were all excluded would be left behind empty.
            if (target != null && !Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
        }
    }

    // ── cooker ───────────────────────────────────────────────────────────────

    private static int RunCooker(string cooker, BuildOptions o, string template, string pakPath)
    {
        var psi = new ProcessStartInfo(cooker)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = o.IniPath != null ? Path.GetDirectoryName(o.IniPath)! : BuildOptions.Cwd
        };

        void Add(params string[] a) { foreach (var x in a) psi.ArgumentList.Add(x); }
        Add("cook", "--content", o.ContentDir, "--out", pakPath, "--manifest", o.Manifest, "--engine-dir", template);
        if (o.CookIni != null) Add("--ini", o.CookIni);
        if (o.AllowCookErrors) Add("--allow-errors");
        if (o.Json) Add("--json");
        if (o.Verbose) Add("--verbose");

        Log.Debug($"{cooker} {string.Join(' ', psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");

        using var p = Process.Start(psi) ?? throw new IOException($"Could not start {cooker}");
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Forward(e.Data, false); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Forward(e.Data, true); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }

    // ── Ossian.ini for the shipped game ──────────────────────────────────────

    private static string BuildIni(BuildOptions o, string appName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"; Generated by OssianBuilder on {DateTime.Now:yyyy-MM-dd HH:mm}. Change Build.ini and rebuild instead of editing this file.");
        sb.AppendLine();
        sb.AppendLine("[App]");
        sb.AppendLine($"Title = \"{appName}\"");
        sb.AppendLine();
        sb.AppendLine("[Content]");
        sb.AppendLine("Mode = pak");
        sb.AppendLine($"Path = \"{o.PakName}\"");
        sb.AppendLine($"ResourcesConfigPath = \"{o.Manifest}\"");
        if (o.TreeConfigPath.Length > 0) sb.AppendLine($"TreeConfigPath = \"{o.TreeConfigPath}\"");
        return sb.ToString();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static int Fail(string message, int code = 2)
    {
        Log.Error("", message);
        return code;
    }

    private static bool IsEmpty(string dir) => !Directory.EnumerateFileSystemEntries(dir).Any();

    // A previous output of this tool always contains the engine assembly.
    private static bool LooksLikeBuild(string dir) => File.Exists(Path.Combine(dir, EngineName + ".dll"));

    private static string Norm(string p)
        => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool SamePath(string a, string b) => string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsInside(string child, string parent)
        => Norm(child).StartsWith(Norm(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}