using System;
using System.Collections.Generic;
using System.Linq;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class InputAxisRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["source", "sensitivity", "invert"];
        public string Source { get; set; } = "";
        public float Sensitivity { get; set; } = 1f;
        public bool Invert { get; set; } = false;

        public override string GetField(string name) => name switch
        {
            "source" => Source,
            "sensitivity" => Sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "invert" => Invert.ToString().ToLower(),
            _ => throw new ArgumentException($"Unknown field '{name}' on InputAxisRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "source": Source = value; break;
                case "sensitivity":
                    Sensitivity = float.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1f;
                    break;
                case "invert":
                    Invert = value == "true";
                    break;
                default:
                    throw new ArgumentException($"Unknown field '{name}' on InputAxisRecord");
            }
        }

        public static InputAxisRecord Create(string id, string source, float sensitivity = 1f, bool invert = false)
            => new InputAxisRecord { Id = id, Source = source, Sensitivity = sensitivity, Invert = invert };
    }

    // ── config ───────────────────────────────────────────────────────────────────

    public class InputAxesConfig : JsonSerialConfig<InputAxisRecord>
    {
        public InputAxesConfig(string id, string path) : base(id, path) { }

        // ── flat-store I/O ────────────────────────────────────────────────────────

        private InputAxisRecord? ReadAxisRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;
            return new InputAxisRecord
            {
                Id = id,
                Source = GetString($"{prefix}.source"),
                Sensitivity = GetFloat($"{prefix}.sensitivity", 1f),
                Invert = GetBool($"{prefix}.invert", false)
            };
        }

        private void WriteAxisRecord(int index, InputAxisRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.source", record.Source);
            Set($"{prefix}.sensitivity", record.Sensitivity);
            Set($"{prefix}.invert", record.Invert);
        }

        // ── records ───────────────────────────────────────────────────────────────

        public new List<InputAxisRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<InputAxisRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadAxisRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        public new InputAxisRecord? GetById(string id)
            => GetAllRecords().FirstOrDefault(r => r.Id == id);
    }
}