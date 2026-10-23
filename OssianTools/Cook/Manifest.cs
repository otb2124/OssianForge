using System.Text.Json;

namespace OssianForge.Tools.Cook;

internal sealed record ManifestRecord(string Id, string Type, List<string> Data);

/// <summary>Reads the engine's resources.json: a root array of { "id", "type", "data": [...] }.</summary>
internal static class Manifest
{
    public static List<ManifestRecord> Read(string fullPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{fullPath}: expected a JSON array at the root.");

        var result = new List<ManifestRecord>();
        int index = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            index++;
            if (el.ValueKind != JsonValueKind.Object) continue;

            string id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            string type = el.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "" : "";
            if (id.Length == 0)
            {
                Log.Warn($"record #{index} has no id, skipped", fullPath);
                continue;
            }

            var data = new List<string>();
            if (el.TryGetProperty("data", out var dEl) && dEl.ValueKind == JsonValueKind.Array)
                foreach (var item in dEl.EnumerateArray())
                    data.Add(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString());

            result.Add(new ManifestRecord(id, type, data));
        }
        return result;
    }
}
