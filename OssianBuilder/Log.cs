using System.Text.Json;

namespace OssianForge.Builder;

/// <summary>
/// Text output for humans, or (with --json) one JSON object per line on stdout so the Manager can parse it:
/// {"event":"stage","name":"cook","index":2,"total":6}. Same shape as OssianCooker's log.
/// </summary>
internal static class Log
{
    public static bool Json;
    public static bool Verbose;
    private static readonly object Gate = new();

    public static void Info(string message) => Emit("info", null, message, Console.Out);
    public static void Debug(string message) { if (Verbose) Emit("debug", null, message, Console.Out); }
    public static void Warn(string message, string? path = null) => Emit("warn", path, message, Console.Error);
    public static void Error(string path, string message) => Emit("error", path, message, Console.Error);

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

    public static void Done(Dictionary<string, object?> summary)
    {
        lock (Gate)
        {
            if (Json)
            {
                summary["event"] = "done";
                WriteJson(summary);
            }
            else
            {
                Console.WriteLine("---");
                foreach (var kv in summary) Console.WriteLine($"{kv.Key}: {kv.Value}");
            }
        }
    }

    /// <summary>
    /// Forwards one output line of the cooker. In JSON mode the line is a cooker event: it is re-emitted with
    /// "stage":"cook" and its "done" renamed to "cook-done" so it cannot be mistaken for the builder's own "done".
    /// </summary>
    public static void Forward(string line, bool isError)
    {
        lock (Gate)
        {
            if (!Json)
            {
                (isError ? Console.Error : Console.Out).WriteLine("   cook | " + line);
                return;
            }

            try
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                if (d != null)
                {
                    var o = new Dictionary<string, object?>();
                    foreach (var kv in d) o[kv.Key] = kv.Value;
                    o["stage"] = "cook";
                    if (o.TryGetValue("event", out var ev) && ev?.ToString() == "done") o["event"] = "cook-done";
                    WriteJson(o);
                    return;
                }
            }
            catch (JsonException) { /* not JSON: fall through and wrap it */ }

            WriteJson(new Dictionary<string, object?>
            {
                ["event"] = isError ? "warn" : "info", ["stage"] = "cook", ["message"] = line
            });
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
        Console.Out.WriteLine(JsonSerializer.Serialize(d));
        Console.Out.Flush();
    }

    public static string FormatSize(long bytes)
        => bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):F1} MB"
         : bytes >= 1 << 10 ? $"{bytes / (double)(1 << 10):F1} KB"
         : $"{bytes} B";
}