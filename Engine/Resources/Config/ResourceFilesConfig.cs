using System;
using System.Collections.Generic;
using System.Linq;

namespace OssianForge.Engine.Resources.Config
{
    public class ResourceFileRecord
    {
        public string Id { get; set; } = "";
        public string Path { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public class ResourceFilesConfig : ConfigFile
    {
        // Flat key format stored in ConfigFile (root is a JSON array, so no "entries" prefix):
        //   "[0].id"   = "shaderfile.basic.vert"
        //   "[0].path" = "ShaderFiles/basic.vert"
        //   "[0].type" = "shaderfile"

        // ── live instance list ───────────────────────────────────────────────────

        /// <summary>
        /// Live <see cref="ResourceFile"/> instances built from the loaded records.
        /// Stays in sync with every mutating operation (Update, Replace, Add, Remove).
        /// </summary>
        public IReadOnlyList<ResourceFile> ResourceFiles => _resourceFiles;
        private readonly List<ResourceFile> _resourceFiles = new();

        public ResourceFilesConfig(string id, string path) : base(id, path, ConfigFormat.Json)
        {
        }

        // ── instance factory ─────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates a <see cref="ResourceFile"/> from a record using the same
        /// type-prefix switch as the old Initialize() method.
        /// </summary>
        private static ResourceFile InstantiateResourceFile(ResourceFileRecord record)
        {
            return record.Type switch
            {
                "shaderfile" => new ShaderFiles.ShaderFile(record.Id, record.Path),
                "meshfile" => new MeshFiles.MeshFile(record.Id, record.Path),
                "texturefile" => new TextureFiles.TextureFile(record.Id, record.Path),
                "animationfile" => new Animations.AnimationFile(record.Id, record.Path),
                "configfile" => new Config.ConfigFile(record.Id, record.Path),
                "scriptfile" => new Scripts.ScriptFile(record.Id, record.Path),
                _ => throw new Exception($"[RESOURCE FILES CONFIG] Unknown resource file type: '{record.Type}' (id: '{record.Id}')")
            };
        }

        // ── build instances from loaded records ──────────────────────────────────

        /// <summary>
        /// Builds (or rebuilds) the live <see cref="ResourceFiles"/> list from the
        /// records currently stored in the config. Call this once after <see cref="ConfigFile.Load"/>.
        /// </summary>
        public void BuildInstances()
        {
            _resourceFiles.Clear();

            foreach (var record in GetAllRecords())
            {
                var instance = InstantiateResourceFile(record);
                _resourceFiles.Add(instance);
            }

            Console.WriteLine($"[RESOURCE FILES CONFIG] Built {_resourceFiles.Count} ResourceFile instance(s).");
        }

        // ── instance lookups ─────────────────────────────────────────────────────

        /// <summary>Returns the live instance with the given id, or null.</summary>
        public ResourceFile? GetInstanceById(string id)
            => _resourceFiles.FirstOrDefault(f => f.Id == id);

        /// <summary>Returns the live instance with the given id cast to <typeparamref name="T"/>, or null.</summary>
        public T? GetInstanceById<T>(string id) where T : ResourceFile
            => _resourceFiles.FirstOrDefault(f => f.Id == id) as T;

        /// <summary>Returns all live instances of type <typeparamref name="T"/>.</summary>
        public List<T> GetInstances<T>() where T : ResourceFile
            => _resourceFiles.OfType<T>().ToList();

        // ── helpers ──────────────────────────────────────────────────────────────

        private int GetLastIndex()
        {
            int max = -1;
            foreach (var key in GetAll().Keys)
            {
                // key format: "[N].field"
                if (key.StartsWith('['))
                {
                    int close = key.IndexOf(']');
                    if (close > 1 && int.TryParse(key[1..close], out int idx))
                        max = Math.Max(max, idx);
                }
            }
            return max;
        }

        private ResourceFileRecord? ReadRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            string path = GetString($"{prefix}.path");
            string type = GetString($"{prefix}.type");

            if (string.IsNullOrEmpty(id))
                return null;

            return new ResourceFileRecord { Id = id, Path = path, Type = type };
        }

        private void WriteRecord(int index, ResourceFileRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.path", record.Path);
            Set($"{prefix}.type", record.Type);
        }

        private int? FindIndex(string id)
        {
            int last = GetLastIndex();
            for (int i = 0; i <= last; i++)
            {
                if (GetString($"[{i}].id") == id)
                    return i;
            }
            return null;
        }

        // ── sync helper ──────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces (or removes) the live instance matching <paramref name="oldId"/>
        /// with a freshly instantiated one built from <paramref name="newRecord"/>.
        /// Pass null for newRecord to only remove.
        /// </summary>
        private void SyncInstance(string oldId, ResourceFileRecord? newRecord)
        {
            int liveIdx = _resourceFiles.FindIndex(f => f.Id == oldId);

            if (newRecord == null)
            {
                if (liveIdx >= 0) _resourceFiles.RemoveAt(liveIdx);
                return;
            }

            var newInstance = InstantiateResourceFile(newRecord);

            if (liveIdx >= 0)
                _resourceFiles[liveIdx] = newInstance;   // replace in-place
            else
                _resourceFiles.Add(newInstance);          // wasn't tracked yet
        }

        // ── record API ───────────────────────────────────────────────────────────

        /// <summary>Returns all resource file records.</summary>
        public List<ResourceFileRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<ResourceFileRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        /// <summary>Returns all records whose type matches <paramref name="type"/> (case-insensitive).</summary>
        public List<ResourceFileRecord> GetAllRecords(string type)
            => GetAllRecords()
               .Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase))
               .ToList();

        /// <summary>Returns the record with the given id, or null if not found.</summary>
        public ResourceFileRecord? GetById(string id)
        {
            int? idx = FindIndex(id);
            return idx.HasValue ? ReadRecord(idx.Value) : null;
        }

        /// <summary>
        /// Returns the record with the given id only if its type matches <typeparamref name="T"/>'s
        /// type-name convention (e.g. <c>ShaderFileRecord</c> → type "shaderfile").
        /// Returns null if not found or type does not match.
        /// </summary>
        public ResourceFileRecord? GetById<T>(string id) where T : ResourceFileRecord
        {
            var record = GetById(id);
            if (record == null) return null;

            string expectedType = typeof(T).Name
                .Replace("FileRecord", "file")
                .Replace("Record", "")
                .ToLower();

            return string.Equals(record.Type, expectedType, StringComparison.OrdinalIgnoreCase)
                ? record
                : null;
        }

        /// <summary>
        /// Updates any combination of id / path / type on the record identified by <paramref name="id"/>.
        /// Pass null to leave a field unchanged.
        /// Also replaces the matching live instance in <see cref="ResourceFiles"/>.
        /// Returns false if the record was not found.
        /// </summary>
        public bool UpdateById(string id, string? newId = null, string? newPath = null, string? newType = null)
        {
            int? idx = FindIndex(id);
            if (!idx.HasValue)
            {
                Console.WriteLine($"[RESOURCE FILES CONFIG] Record '{id}' not found.");
                return false;
            }

            var record = ReadRecord(idx.Value)!;
            if (newId != null) record.Id = newId;
            if (newPath != null) record.Path = newPath;
            if (newType != null) record.Type = newType;

            WriteRecord(idx.Value, record);
            SyncInstance(id, record);
            return true;
        }

        /// <summary>
        /// Replaces the record identified by <paramref name="id"/> with <paramref name="record"/>.
        /// Also replaces the matching live instance in <see cref="ResourceFiles"/>.
        /// Returns false if the record was not found.
        /// </summary>
        public bool ReplaceById(string id, ResourceFileRecord record)
        {
            int? idx = FindIndex(id);
            if (!idx.HasValue)
            {
                Console.WriteLine($"[RESOURCE FILES CONFIG] Record '{id}' not found.");
                return false;
            }

            WriteRecord(idx.Value, record);
            SyncInstance(id, record);
            return true;
        }

        /// <summary>
        /// Appends a new record and adds a live instance to <see cref="ResourceFiles"/>.
        /// Throws if a record with the same id already exists.
        /// </summary>
        public void Add(ResourceFileRecord record)
        {
            if (FindIndex(record.Id).HasValue)
                throw new InvalidOperationException($"Record with id '{record.Id}' already exists.");

            int next = GetLastIndex() + 1;
            WriteRecord(next, record);
            SyncInstance(record.Id, record);
        }

        /// <summary>
        /// Removes the record with the given id, shifts remaining entries down,
        /// and removes the matching live instance from <see cref="ResourceFiles"/>.
        /// </summary>
        public bool RemoveById(string id)
        {
            int? idx = FindIndex(id);
            if (!idx.HasValue)
            {
                Console.WriteLine($"[RESOURCE FILES CONFIG] Record '{id}' not found.");
                return false;
            }

            int last = GetLastIndex();

            for (int i = idx.Value; i < last; i++)
            {
                var next = ReadRecord(i + 1)!;
                WriteRecord(i, next);
            }

            string lastPrefix = $"[{last}]";
            Set($"{lastPrefix}.id", "");
            Set($"{lastPrefix}.path", "");
            Set($"{lastPrefix}.type", "");

            SyncInstance(id, null);
            return true;
        }
    }
}