using OssianForge.Engine.Resources.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class ResourceFileRecord : ConfigRecord
    {
        public string Path { get; set; } = "";
        public string Type { get; set; } = "";

        public override IEnumerable<string> FieldNames { get; } = ["path", "type"];

        public override string GetField(string name) => name switch
        {
            "path" => Path,
            "type" => Type,
            _ => throw new ArgumentException($"Unknown field '{name}' on ResourceFileRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "path": Path = value; break;
                case "type": Type = value; break;
                default: throw new ArgumentException($"Unknown field '{name}' on ResourceFileRecord");
            }
        }
    }


    public interface IResourceFilePack
    {
        ResourceFile GetByIdBase(string id);
        IEnumerable<ResourceFile> GetAllFiles();
    }

    // ── config ───────────────────────────────────────────────────────────────────

    public class ResourceFilesConfig : JsonSerialConfig<ResourceFileRecord>
    {
        // ── live instance list ───────────────────────────────────────────────────

        /// <summary>
        /// Live <see cref="ResourceFile"/> instances built from the loaded records.
        /// Stays in sync with every mutating operation (Replace, Add, Remove).
        /// </summary>
        public IReadOnlyList<ResourceFile> ResourceFiles => _resourceFiles;
        private readonly List<ResourceFile> _resourceFiles = new();

        public ResourceFilesConfig(string id, string path)
            : base(id, path) { }

        // ── instance factory ─────────────────────────────────────────────────────

        private static ResourceFile InstantiateResourceFile(ResourceFileRecord record)
        {
            var type = GetResourceFileType(record.Type);
            return (ResourceFile)Activator.CreateInstance(type, record.Id, record.Path)!;
        }

        private static Type GetResourceFileType(string type)
        {
            if (type.StartsWith("filepack."))
            {
                string innerType = type["filepack.".Length..];
                Type innerFileType = GetResourceFileType(innerType);
                return typeof(ResourceFilePack<>).MakeGenericType(innerFileType);
            }

            return type switch
            {
                "shaderfile" => typeof(ShaderFiles.ShaderFile),
                "meshfile" => typeof(MeshFiles.MeshFile),
                "texturefile" => typeof(TextureFiles.TextureFile),
                "animationfile" => typeof(Animations.AnimationFile),
                "configfile" => typeof(Config.ConfigFile),
                "script" => typeof(Scripts.ScriptFile),
                _ => throw new Exception($"[RESOURCE FILES CONFIG] Unknown type '{type}'")
            };
        }


        // ── build ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds (or rebuilds) the live <see cref="ResourceFiles"/> list from the
        /// records in the config. Call once after <see cref="ConfigFile.Load"/>.
        /// </summary>
        public void BuildInstances()
        {
            _resourceFiles.Clear();
            foreach (var record in GetAllRecords())
                _resourceFiles.Add(InstantiateResourceFile(record));

            Console.WriteLine($"[RESOURCE FILES CONFIG] Built {_resourceFiles.Count} ResourceFile instance(s).");
        }

        // ── instance lookups ─────────────────────────────────────────────────────

        /// <summary>Returns the live instance with the given id, or null. Also searches inside packs.</summary>
        public ResourceFile? GetInstanceById(string id)
        {
            var direct = _resourceFiles.FirstOrDefault(f => f.Id == id);
            if (direct != null) return direct;

            foreach (var pack in _resourceFiles.OfType<IResourceFilePack>())
            {
                var found = pack.GetByIdBase(id);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>Returns the live instance with the given id cast to <typeparamref name="T"/>, or null. Also searches inside packs.</summary>
        public T? GetInstanceById<T>(string id) where T : ResourceFile
        {
            var direct = _resourceFiles.FirstOrDefault(f => f.Id == id) as T;
            if (direct != null) return direct;

            foreach (var pack in _resourceFiles.OfType<IResourceFilePack>())
            {
                var found = pack.GetByIdBase(id) as T;
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>Returns all live instances of type <typeparamref name="T"/>. Also searches inside packs.</summary>
        public List<T> GetInstances<T>() where T : ResourceFile
        {
            var results = _resourceFiles.OfType<T>().ToList();

            foreach (var pack in _resourceFiles.OfType<IResourceFilePack>())
                results.AddRange(pack.GetAllFiles().OfType<T>());

            return results;
        }

        // ── sync hooks ───────────────────────────────────────────────────────────

        protected override void OnRecordReplaced(string oldId, ResourceFileRecord newRecord)
        {
            int liveIdx = _resourceFiles.FindIndex(f => f.Id == oldId);
            var newInstance = InstantiateResourceFile(newRecord);
            if (liveIdx >= 0) _resourceFiles[liveIdx] = newInstance;
            else _resourceFiles.Add(newInstance);
        }

        protected override void OnRecordAdded(ResourceFileRecord record)
            => _resourceFiles.Add(InstantiateResourceFile(record));

        protected override void OnRecordRemoved(string id)
        {
            int liveIdx = _resourceFiles.FindIndex(f => f.Id == id);
            if (liveIdx >= 0) _resourceFiles.RemoveAt(liveIdx);
        }



        public ScriptFile FindScriptFile(string packOrFileId, string typeName)
        {
            // direct script file
            var direct = GetInstanceById<ScriptFile>(packOrFileId);
            if (direct != null) return direct;

            // search inside pack by filename stem
            var pack = GetInstanceById<ResourceFilePack<ScriptFile>>(packOrFileId);
            if (pack != null)
            {
                var script = pack.Files.FirstOrDefault(f =>
                    System.IO.Path.GetFileNameWithoutExtension(f.Path) == typeName);
                if (script != null) return script;
            }

            throw new Exception($"Could not find script '{typeName}' in '{packOrFileId}'");
        }
    }
}