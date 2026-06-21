using OssianForge.Engine.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public enum InputKeyType
    {
        Click,    // edge-triggered: true for one frame on press
        Down,     // level-triggered: true while held
        Release,  // edge-triggered: true for one frame on release
    }

    public class InputKeyRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["type", "keys"];

        public InputKeyType Type { get; set; } = InputKeyType.Down;

        // Stored as a JSON array string, e.g. '["W","ArrowUp"]'
        public string KeysJson { get; set; } = "[]";

        public List<string> Keys
            => JsonSerializer.Deserialize<List<string>>(KeysJson) ?? new();

        public override string GetField(string name) => name switch
        {
            "type" => Type.ToString().ToLowerInvariant(),
            "keys" => KeysJson,
            _ => throw new ArgumentException($"Unknown field '{name}' on InputKeyRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "type": Type = ParseType(value); break;
                case "keys": KeysJson = string.IsNullOrWhiteSpace(value) ? "[]" : value; break;
                default: throw new ArgumentException($"Unknown field '{name}' on InputKeyRecord");
            }
        }

        private static InputKeyType ParseType(string value) => value.ToLowerInvariant() switch
        {
            "click" => InputKeyType.Click,
            "down" => InputKeyType.Down,
            "release" => InputKeyType.Release,
            _ => throw new ArgumentException($"Unknown input key type '{value}'")
        };

        public static InputKeyRecord Create(string id, InputKeyType type, IEnumerable<string> keys)
            => new InputKeyRecord { Id = id, Type = type, KeysJson = JsonSerializer.Serialize(keys) };
    }

    // ── config ───────────────────────────────────────────────────────────────────

    public class InputKeysConfig : JsonSerialConfig<InputKeyRecord>
    {
        public InputKeysConfig(string id, string path) : base(id, path) { }

        // ── flat-store I/O ────────────────────────────────────────────────────────

        private InputKeyRecord? ReadInputKeyRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = new InputKeyRecord
            {
                Id = id,
                Type = ParseTypeOrDefault(GetString($"{prefix}.type"))
            };

            var keys = new List<string>();
            int i = 0;
            while (true)
            {
                string val = GetString($"{prefix}.keys[{i}]");
                if (string.IsNullOrEmpty(val)) break;
                keys.Add(val);
                i++;
            }
            record.KeysJson = JsonSerializer.Serialize(keys);

            return record;
        }

        private void WriteInputKeyRecord(int index, InputKeyRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.type", record.Type.ToString().ToLowerInvariant());

            var keys = record.Keys;
            for (int i = 0; i < keys.Count; i++)
                Set($"{prefix}.keys[{i}]", keys[i]);
        }

        private static InputKeyType ParseTypeOrDefault(string value) => value.ToLowerInvariant() switch
        {
            "click" => InputKeyType.Click,
            "release" => InputKeyType.Release,
            _ => InputKeyType.Down
        };

        // ── records ───────────────────────────────────────────────────────────────

        public new List<InputKeyRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<InputKeyRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadInputKeyRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        public new InputKeyRecord? GetById(string id)
            => GetAllRecords().FirstOrDefault(r => r.Id == id);

        // ── binding lookup ────────────────────────────────────────────────────────

        public List<KeyHandler.InputKey> ResolveBindings(InputKeyRecord record)
            => record.Keys.Select(ParseKeyName).ToList();

        private static KeyHandler.InputKey ParseKeyName(string name)
        {
            if (TryParseMouseButton(name, out var mouseButton))
                return new KeyHandler.InputKey(mouseButton);

            if (Enum.TryParse<Silk.NET.Input.Key>(name, ignoreCase: true, out var key))
                return new KeyHandler.InputKey(key);

            throw new Exception($"[INPUT KEYS CONFIG] Unknown key name '{name}'.");
        }

        private static bool TryParseMouseButton(string name, out MouseInput.MouseButtons button)
        {
            switch (name.ToLowerInvariant())
            {
                case "mouseleft": button = MouseInput.MouseButtons.Left; return true;
                case "mouseright": button = MouseInput.MouseButtons.Right; return true;
                case "mousemiddle": button = MouseInput.MouseButtons.Middle; return true;
                default: button = default; return false;
            }
        }

    }

    public static class InputStateStore
    {
        private static readonly Dictionary<string, bool> _activeStates = new();

        public static void Set(string id, bool value) => _activeStates[id] = value;
        public static bool IsActive(string id) => _activeStates.TryGetValue(id, out bool v) && v;
        public static void Clear() => _activeStates.Clear();
    }
}