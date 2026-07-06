using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Fonts;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Engine.Resources.Scripts;
using OssianForge.Engine.Resources.ShaderFiles;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Sounds;
using OssianForge.Engine.Resources.TextureFiles;
using OssianForge.Engine.Resources.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class ResourceRecord : ConfigRecord
    {
        public List<string> Data { get; set; } = new();
        public string Type { get; set; } = "";

        public override IEnumerable<string> FieldNames { get; } = ["type"];

        public override string GetField(string name) => name switch
        {
            "type" => Type,
            _ => throw new ArgumentException($"Unknown field '{name}' on ResourceRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "type": Type = value; break;
                default: throw new ArgumentException($"Unknown field '{name}' on ResourceRecord");
            }
        }
    }

    // ── config ───────────────────────────────────────────────────────────────────
    public class ResourcesConfig : JsonSerialConfig<ResourceRecord>
    {
        // ── live instance list ───────────────────────────────────────────────────

        public IReadOnlyList<Resource> Resources => _resources;
        private readonly List<Resource> _resources = new();

        public ResourcesConfig(string id, string path)
            : base(id, path) { }

        // ── record I/O override (data is a list, needs special handling) ─────────

        protected override ResourceRecord CreateRecord() => new ResourceRecord();

        private ResourceRecord? ReadResourceRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = new ResourceRecord();
            record.Id = id;
            record.Type = GetString($"{prefix}.type");

            // read data array: [index].data[0], [index].data[1], ...
            int i = 0;
            while (true)
            {
                string val = GetString($"{prefix}.data[{i}]");
                if (string.IsNullOrEmpty(val)) break;
                record.Data.Add(val);
                i++;
            }

            return record;
        }

        private void WriteResourceRecord(int index, ResourceRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.type", record.Type);
            for (int i = 0; i < record.Data.Count; i++)
                Set($"{prefix}.data[{i}]", record.Data[i]);
        }

        // ── override GetAllRecords to use our custom reader ───────────────────────

        public new List<ResourceRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<ResourceRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadResourceRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        // ── instance factory ─────────────────────────────────────────────────────

        private static Resource InstantiateResource(ResourceRecord record) =>
            record.Type switch
            {
                "meshfile" => new MeshFile(record.Id, record.Data[0]),
                "texturefile" => new TextureFile(record.Id, record.Data[0]),
                "shaderfile" => new ShaderFile(record.Id, record.Data[0]),
                "animationfile" => new AnimationFile(record.Id, record.Data[0]),
                "configfile.font" => new ConfigFile(record.Id, record.Data[0]),
                "configfile.scene" => new SceneConfig(record.Id, record.Data[0]),
                "configfile.tree" => new TreeConfig(record.Id, record.Data[0]),
                "configfile.actions" => new ActionsConfig(record.Id, record.Data[0]),
                "configfile.pronouns" => new PronounsConfig(record.Id, record.Data[0]),
                "configfile.inputKeys" => new InputKeysConfig(record.Id, record.Data[0]),
                "configfile.inputAxes" => new InputAxesConfig(record.Id, record.Data[0]),
                "configfile.statemachine" => new StateMachineConfig(record.Id, record.Data[0]),
                "scriptfile" => new ScriptFile(record.Id, record.Data[0]),
                "soundfile" => new SoundFile(record.Id, record.Data[0]),

                "mesh" => new MeshResource(record.Id, record.Data[0]),
                "terrainmesh" => new HeightmapMeshResource(record.Id, record.Data[0]),
                "animation" => new AnimationResource(record.Id, record.Data.ToArray()),
                "basicshader" => new BasicShaderResource(record.Id, record.Data[0], record.Data[1]),
                "wireframeshader" => new WireframeShaderResource(record.Id, record.Data[0], record.Data[1]),
                "skyboxshader" => new SkyboxShaderResource(record.Id, record.Data[0], record.Data[1]),
                "shader" => new ShaderResource(record.Id, record.Data[0], record.Data[1]),
                "sdfshader" => new SdfShaderResource(record.Id, record.Data[0], record.Data[1]),
                "texture" => new TextureResource(record.Id, record.Data.ToArray()),
                "cubemaptexture" => new CubemapTextureResource(record.Id, record.Data[0], record.Data[1], record.Data[2], record.Data[3], record.Data[4], record.Data[5]),
                "meshcollider" => new MeshColliderResource(record.Id, record.Data[0]),
                "capsulecollider" => new CapsuleColliderResource(record.Id, ToFloat(record.Data[0]), ToFloat(record.Data[1])),
                "boxcollider" => new BoxColliderResource(record.Id, new Vector3(ToFloat(record.Data[0]), ToFloat(record.Data[1]), ToFloat(record.Data[2]))),
                "terraincollider" => new TerrainColliderResource(record.Id, record.Data[0]),
                "font" => new FontResource(record.Id, record.Data[0], record.Data[1]),
                "sound" => new SoundResource(record.Id, record.Data[0]),
                _ => throw new Exception($"[RESOURCES CONFIG] Unknown type '{record.Type}' (id: '{record.Id}')")
            };

        // ── build ────────────────────────────────────────────────────────────────

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

        public void BuildInstances()
        {
            _resources.Clear();
            foreach (var record in GetAllRecords())
            {
                _resources.Add(InstantiateResource(record));
            }

            Console.WriteLine($"[RESOURCES CONFIG] Built {_resources.Count} Resource instance(s).");
        }

        public void BuildInstances<T>() where T : Resource
        {
            int count = 0;
            foreach (var record in GetAllRecords())
            {
                var instance = InstantiateResource(record);
                if (instance is not T) continue;

                int idx = _resources.FindIndex(r => r.Id == record.Id);
                if (idx >= 0) _resources[idx] = instance;
                else _resources.Add(instance);

                count++;
            }
            Console.WriteLine($"[RESOURCES CONFIG] Built {count} {typeof(T).Name} instance(s).");
        }

        public void BuildInstances(params string[] ids)
        {
            var idSet = new HashSet<string>(ids);
            var records = GetAllRecords().Where(r => idSet.Contains(r.Id));

            foreach (var record in records)
            {
                var instance = InstantiateResource(record);
                int idx = _resources.FindIndex(r => r.Id == record.Id);
                if (idx >= 0) _resources[idx] = instance;
                else _resources.Add(instance);
            }

            Console.WriteLine($"[RESOURCES CONFIG] Built {ids.Length} Resource instance(s) by id.");
        }

        // ── instance lookups ─────────────────────────────────────────────────────

        public Resource? GetInstanceById(string id)
            => _resources.FirstOrDefault(r => r.Id == id);

        public T? GetInstanceById<T>(string id) where T : Resource
            => _resources.FirstOrDefault(r => r.Id == id) as T;

        public List<T> GetInstances<T>() where T : Resource
            => _resources.OfType<T>().ToList();

        // ── sync hooks ───────────────────────────────────────────────────────────

        protected override void OnRecordReplaced(string oldId, ResourceRecord newRecord)
        {
            int liveIdx = _resources.FindIndex(r => r.Id == oldId);
            var newInstance = InstantiateResource(newRecord);
            if (liveIdx >= 0) _resources[liveIdx] = newInstance;
            else _resources.Add(newInstance);
        }

        protected override void OnRecordAdded(ResourceRecord record)
            => _resources.Add(InstantiateResource(record));

        protected override void OnRecordRemoved(string id)
        {
            int liveIdx = _resources.FindIndex(r => r.Id == id);
            if (liveIdx >= 0) _resources.RemoveAt(liveIdx);
        }


        public void LoadResources()
        {
            foreach (var resource in _resources)
                resource.Load();

            Console.WriteLine($"[RESOURCES CONFIG] Loaded {_resources.Count} Resource instance(s).");
        }

        public void LoadResources<T>() where T : Resource
        {
            var targets = _resources.OfType<T>().ToList();

            foreach (var resource in targets)
                resource.Load();

            Console.WriteLine($"[RESOURCES CONFIG] Loaded {targets.Count} {typeof(T).Name} instance(s).");
        }

        public void LoadResources(params string[] ids)
        {
            foreach (var id in ids)
            {
                var instance = GetInstanceById(id);
                if (instance == null)
                {
                    Console.WriteLine($"[RESOURCES CONFIG] Instance '{id}' not found, skipping.");
                    continue;
                }
                instance.Load();
            }
            Console.WriteLine($"[RESOURCES CONFIG] Loaded {ids.Length} Resource instance(s) by id.");
        }

        public static float ToFloat(string input)
        {
            return float.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}