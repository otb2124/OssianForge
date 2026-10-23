using System.Text.Json;

namespace OssianForge.Tools;

/// <summary>
/// Text output for humans, or (with --json) one JSON object per line on stdout so the
/// Manager's Rust backend can parse progress: {"event":"asset",...} / {"event":"stage",...}.
/// </summary>
internal static class Log
{
    public static bool Json;
    public static bool Verbose;

    /// <summary>Set while the cooker runs inside `build`: every JSON event then also carries "stage":"cook".</summary>
    public static string? StageTag;
    private static readonly object Gate = new();

    public static void Info(string message) => Emit("info", null, message, Console.Out);
    public static void Debug(string message) { if (Verbose) Emit("debug", null, message, Console.Out); }
    public static void Warn(string message, string? path = null) => Emit("warn", path, message, Console.Error);
    public static void Error(string path, string message) => Emit("error", path, message, Console.Error);

    public static void Asset(string path, string type, string status, long bytes, long ms)
    {
        lock (Gate)
        {
            if (Json)
                WriteJson(new Dictionary<string, object?>
                {
                    ["event"] = "asset", ["path"] = path, ["type"] = type,
                    ["status"] = status, ["bytes"] = bytes, ["ms"] = ms
                });
            else
                Console.WriteLine($"[{status,-6}] {type,-9} {path}  ({FormatSize(bytes)}, {ms} ms)");
        }
    }

    public static void Progress(int done, int total)
    {
        if (!Json) return;
        lock (Gate) WriteJson(new Dictionary<string, object?> { ["event"] = "progress", ["done"] = done, ["total"] = total });
    }

    public static void Stage(string name, int index, int total)
    {
        lock (Gate)
        {
            if (Json)
                WriteJson(new Dictionary<string, object?> { ["event"] = "stage", ["name"] = name, ["index"] = index, ["total"] = total });
            else
                Console.WriteLine($"[{index}/{total}] {name}");
        }
    }

    public static void Done(Dictionary<string, object?> summary, string evt = "done")
    {
        lock (Gate)
        {
            if (Json)
            {
                summary["event"] = evt;
                WriteJson(summary);
            }
            else
            {
                Console.WriteLine("---");
                foreach (var kv in summary) Console.WriteLine($"{kv.Key}: {kv.Value}");
            }
        }
    }

    private static void Emit(string level, string? path, string message, TextWriter textOut)
    {
        lock (Gate)
        {
            if (Json)
            {
                var d = new Dictionary<string, object?> { ["event"] = level, ["message"] = message };
                if (!string.IsNullOrEmpty(path)) d["path"] = path;
                WriteJson(d);
            }
            else
            {
                string prefix = level switch { "warn" => "warning: ", "error" => "error: ", _ => "" };
                textOut.WriteLine(!string.IsNullOrEmpty(path) ? $"{prefix}{path}: {message}" : $"{prefix}{message}");
            }
        }
    }

    private static void WriteJson(Dictionary<string, object?> d)
    {
        if (StageTag != null) d.TryAdd("stage", StageTag);
        Console.Out.WriteLine(JsonSerializer.Serialize(d));
        Console.Out.Flush();
    }

    public static string FormatSize(long bytes)
        => bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):F1} MB"
         : bytes >= 1 << 10 ? $"{bytes / (double)(1 << 10):F1} KB"
         : $"{bytes} B";
}
