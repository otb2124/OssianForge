using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class PronounRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["target", "pronouns"];

        public string Target { get; set; } = "";
        public string PronounsJson { get; set; } = "[]";

        public List<string> Pronouns
            => JsonSerializer.Deserialize<List<string>>(PronounsJson) ?? new();

        public override string GetField(string name) => name switch
        {
            "target" => Target,
            "pronouns" => PronounsJson,
            _ => throw new ArgumentException($"Unknown field '{name}' on PronounRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "target": Target = value; break;
                case "pronouns": PronounsJson = string.IsNullOrWhiteSpace(value) ? "[]" : value; break;
                default: throw new ArgumentException($"Unknown field '{name}' on PronounRecord");
            }
        }

        public static PronounRecord Create(string id, string target, IEnumerable<string> pronouns)
            => new PronounRecord { Id = id, Target = target, PronounsJson = JsonSerializer.Serialize(pronouns) };
    }

    // ── config ───────────────────────────────────────────────────────────────────

    public class PronounsConfig : JsonSerialConfig<PronounRecord>
    {
        public PronounsConfig(string id, string path) : base(id, path) { }




        public override void Load()
        {
            base.Load();
            PronounResolver.Merge(this);
        }



        // ── flat-store I/O ────────────────────────────────────────────────────────

        private PronounRecord? ReadPronounRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = new PronounRecord
            {
                Id = id,
                Target = GetString($"{prefix}.target")
            };

            var pronouns = new List<string>();
            int i = 0;
            while (true)
            {
                string val = GetString($"{prefix}.pronouns[{i}]");
                if (string.IsNullOrEmpty(val)) break;
                pronouns.Add(val);
                i++;
            }
            record.PronounsJson = JsonSerializer.Serialize(pronouns);

            return record;
        }

        private void WritePronounRecord(int index, PronounRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.target", record.Target);

            var pronouns = record.Pronouns;
            for (int i = 0; i < pronouns.Count; i++)
                Set($"{prefix}.pronouns[{i}]", pronouns[i]);
        }

        // ── records ───────────────────────────────────────────────────────────────

        public new List<PronounRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<PronounRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadPronounRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        public new PronounRecord? GetById(string id)
            => GetAllRecords().FirstOrDefault(r => r.Id == id);
    }


    public static class PronounResolver
    {
        private static readonly Dictionary<string, string> _map = new();

        /// <summary>Merges one config's pronouns into the map without clearing existing entries.</summary>
        public static void Merge(PronounsConfig config)
        {
            foreach (var record in config.GetAllRecords())
                foreach (var pronoun in record.Pronouns)
                    _map[pronoun] = record.Target;

            Console.WriteLine($"[PRONOUN RESOLVER] Merged '{config.Id}' — map now has {_map.Count} pronoun(s).");
        }

        /// <summary>Rebuilds the whole map from scratch across all given configs — use for a full reload.</summary>
        public static void BuildMap(IEnumerable<PronounsConfig> configs)
        {
            _map.Clear();
            foreach (var config in configs)
                Merge(config);
        }

        public static void Set(string pronoun, string target)
        {
            _map[pronoun] = target;
            Console.WriteLine($"[PRONOUN RESOLVER] Set '{pronoun}' → '{target}'");
        }

        public static void Remove(string pronoun) => _map.Remove(pronoun);
        public static void Clear() => _map.Clear();

        public static string Resolve(string call)
            => _map.TryGetValue(call, out var target) ? target : call;
    }
}