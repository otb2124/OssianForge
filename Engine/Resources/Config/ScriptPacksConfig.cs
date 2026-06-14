// ── record ───────────────────────────────────────────────────────────────────

using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Resources.Scripts;
using OssianForge.Engine.Resources;
using OssianForge.Engine;

public class ScriptPackRecord : ConfigRecord
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Description { get; set; } = "";

    public override IEnumerable<string> FieldNames { get; } = ["name", "path", "description"];

    public override string GetField(string name) => name switch
    {
        "name" => Name,
        "path" => Path,
        "description" => Description,
        _ => throw new ArgumentException($"Unknown field '{name}' on ScriptPackRecord")
    };

    public override void SetField(string name, string value)
    {
        switch (name)
        {
            case "name": Name = value; break;
            case "path": Path = value; break;
            case "description": Description = value; break;
            default: throw new ArgumentException($"Unknown field '{name}' on ScriptPackRecord");
        }
    }
}

// ── config ───────────────────────────────────────────────────────────────────

public class ScriptPacksConfig : JsonConfigFile<ScriptPackRecord>
{
    // ── live instance list ───────────────────────────────────────────────────

    public IReadOnlyList<ScriptFile> ScriptFiles => _scriptFiles;
    private readonly List<ScriptFile> _scriptFiles = new();

    public ScriptPacksConfig(string id, string path)
        : base(id, path) { }

    // ── build ────────────────────────────────────────────────────────────────

    public void BuildInstances()
    {
        _scriptFiles.Clear();
        foreach (var record in GetAllRecords())
            foreach (var scriptFile in InstantiateScriptFiles(record))
                _scriptFiles.Add(scriptFile);

        Console.WriteLine($"[SCRIPT PACKS CONFIG] Built {_scriptFiles.Count} ScriptFile instance(s).");
    }

    private void SyncToResourceFilesConfig()
    {
        foreach (var scriptFile in _scriptFiles)
        {
            var record = new ResourceFileRecord
            {
                Id = scriptFile.Id,
                Path = scriptFile.Path,
                Type = "scriptfile"
            };

            var existing = Engine.Resources.ResourceLoader.ResourceFilesConfig.GetById(scriptFile.Id);
            if (existing == null)
                Engine.Resources.ResourceLoader.ResourceFilesConfig.Add(record);
            else if (existing.Path != record.Path || existing.Type != record.Type)
                Engine.Resources.ResourceLoader.ResourceFilesConfig.ReplaceById(scriptFile.Id, record);
        }
    }

    public override void Load()
    {
        base.Load();

        BuildInstances();
        SyncToResourceFilesConfig();
    }

    // ── instance factory ─────────────────────────────────────────────────────

    private static List<ScriptFile> InstantiateScriptFiles(ScriptPackRecord record)
    {
        string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + record.Path;

        if (!Directory.Exists(globalPath))
        {
            Console.WriteLine($"[SCRIPT PACKS CONFIG] Directory not found for pack '{record.Id}': '{globalPath}'");
            return new List<ScriptFile>();
        }

        return Directory
            .GetFiles(globalPath, "*.cs", SearchOption.AllDirectories)
            .Select(fullPath =>
            {
                // make path relative to content folder, with forward slashes
                string relativePath = System.IO.Path.GetRelativePath(
                    ResourceFile.CONTENT_FOLDER_PATH, fullPath)
                    .Replace('\\', '/');

                // id: e.g. "state_machine.StateMachineProperty"
                string fileName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                string scriptId = $"{record.Id}.{fileName}";

                return new ScriptFile(scriptId, relativePath);
            })
            .ToList();
    }

    // ── instance lookups ─────────────────────────────────────────────────────

    public ScriptFile? GetInstanceById(string id)
        => _scriptFiles.FirstOrDefault(f => f.Id == id);

    public List<ScriptFile> GetInstancesByPack(string packId)
        => _scriptFiles.Where(f => f.Id.StartsWith(packId + ".")).ToList();

    // ── sync hooks ───────────────────────────────────────────────────────────

    protected override void OnRecordReplaced(string oldId, ScriptPackRecord newRecord)
    {
        _scriptFiles.RemoveAll(f => f.Id.StartsWith(oldId + "."));
        foreach (var scriptFile in InstantiateScriptFiles(newRecord))
            _scriptFiles.Add(scriptFile);
    }

    protected override void OnRecordAdded(ScriptPackRecord record)
    {
        foreach (var scriptFile in InstantiateScriptFiles(record))
            _scriptFiles.Add(scriptFile);
    }

    protected override void OnRecordRemoved(string id)
        => _scriptFiles.RemoveAll(f => f.Id.StartsWith(id + "."));
}