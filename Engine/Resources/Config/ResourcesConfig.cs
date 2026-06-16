using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Fonts;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Sounds;
using OssianForge.Engine.Resources.Textures;
using System;
using System.Collections.Generic;
using System.Linq;

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
                "mesh" => new MeshResource(record.Id, record.Data[0]),
                "terrainmesh" => new TerrainMeshResource(record.Id, record.Data[0]),
                "animation" => new AnimationResource(record.Id, record.Data.ToArray()),
                "basicshader" => new BasicShaderResource(record.Id, record.Data[0], record.Data[1]),
                "skyboxshader" => new SkyboxShaderResource(record.Id, record.Data[0], record.Data[1]),
                "shader" => new ShaderResource(record.Id, record.Data[0], record.Data[1]),
                "sdfshader" => new SdfShaderResource(record.Id, record.Data[0], record.Data[1]),
                "texture" => new TextureResource(record.Id, record.Data.ToArray()),
                "cubemaptexture" => new CubemapTextureResource(record.Id, record.Data[0], record.Data[1], record.Data[2], record.Data[3], record.Data[4], record.Data[5]),
                "collider" => new ColliderResource(record.Id, record.Data[0]),
                "font" => new FontResource(record.Id, record.Data[0], record.Data[1]),
                "sound" => new SoundResource(record.Id, record.Data[0]),
                _ => throw new Exception($"[RESOURCES CONFIG] Unknown type '{record.Type}' (id: '{record.Id}')")
            };

        // ── build ────────────────────────────────────────────────────────────────

        public void BuildInstances()
        {
            _resources.Clear();
            foreach (var record in GetAllRecords())
                _resources.Add(InstantiateResource(record));

            Console.WriteLine($"[RESOURCES CONFIG] Built {_resources.Count} Resource instance(s).");
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
    }
}