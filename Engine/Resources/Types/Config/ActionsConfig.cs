using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class ActionRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["call", "args"];
        public string Call { get; set; } = "";

        // Stored as a JSON array string in the flat store: e.g. '["scene.gameplay"]'
        public string ArgsJson { get; set; } = "[]";

        // ── stable flat-store encoding ────────────────────────────────────────────────

        private static string EncodeArgs(string json)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        private static string DecodeArgs(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return "[]";
            try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
            catch { return "[]"; }
        }

        public List<JsonElement> Args
            => JsonSerializer.Deserialize<List<JsonElement>>(ArgsJson) ?? new();

        // Override GetField/SetField to encode on write, decode on read
        public override string GetField(string name) => name switch
        {
            "call" => Call,
            "args" => EncodeArgs(ArgsJson),
            _ => throw new ArgumentException($"Unknown field '{name}' on ActionRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "call": Call = value; break;
                case "args":
                    ArgsJson = DecodeArgs(value);
                    Console.WriteLine($"[ACTION RECORD] SetField args raw='{value}' decoded='{ArgsJson}'");
                    break;
                default: throw new ArgumentException($"Unknown field '{name}' on ActionRecord");
            }
        }

        // ── factory helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Constructs an ActionRecord from a typed args list, serializing it to JSON.
        /// </summary>
        public static ActionRecord Create(string id, string call, IEnumerable<object?> args)
            => new ActionRecord { Id = id, Call = call, ArgsJson = JsonSerializer.Serialize(args) };
    }


    // ── config ───────────────────────────────────────────────────────────────────

    public class ActionsConfig : JsonSerialConfig<ActionRecord>
    {
        public ActionsConfig(string id, string path)
            : base(id, path) { }

        // ── custom flat-store I/O ────────────────────────────────────────────────────

        private ActionRecord? ReadActionRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = new ActionRecord
            {
                Id = id,
                Call = GetString($"{prefix}.call")
            };

            var args = new List<string>();
            int i = 0;
            while (true)
            {
                string val = GetString($"{prefix}.args[{i}]");
                if (string.IsNullOrEmpty(val)) break;
                args.Add(val);
                i++;
            }
            // Re-serialize the flat strings back into a JSON array ArgsJson can deserialize
            record.ArgsJson = System.Text.Json.JsonSerializer.Serialize(args);

            return record;
        }

        private void WriteActionRecord(int index, ActionRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.call", record.Call);

            var args = record.Args;
            for (int i = 0; i < args.Count; i++)
                Set($"{prefix}.args[{i}]", UnboxToString(args[i]));
        }

        // ── override GetAllRecords ───────────────────────────────────────────────────

        public new List<ActionRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<ActionRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadActionRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        public new ActionRecord? GetById(string id)
            => GetAllRecords().FirstOrDefault(r => r.Id == id);

        // ── convenience lookups ──────────────────────────────────────────────────────

        public List<ActionRecord> GetByCall(string call)
            => GetAllRecords().Where(r => r.Call == call).ToList();

        // ── execution ────────────────────────────────────────────────────────────────

        public void Execute(string id)
        {
            var record = GetById(id)
                ?? throw new Exception($"[ACTIONS CONFIG] Action '{id}' not found.");

            Invoke(record.Call, record.Args);
        }

        public void ExecuteAll(IEnumerable<string> ids)
        {
            foreach (var id in ids)
                Execute(id);
        }

        // ── reflection dispatch ──────────────────────────────────────────────────────

        private static void Invoke(string call, List<JsonElement> args)
        {
            int lastDot = call.LastIndexOf('.');
            if (lastDot < 0)
                throw new Exception($"[ACTIONS CONFIG] Invalid call format '{call}'.");

            string typeName = call[..lastDot];
            string methodName = call[(lastDot + 1)..];

            var targetType = Type.GetType(typeName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(a => a.GetType(typeName))
                       .FirstOrDefault(t => t != null)
                ?? throw new Exception($"[ACTIONS CONFIG] Type '{typeName}' not found.");

            object?[] unboxed = args.Select(UnboxJsonElement).ToArray();
            Type[] argTypes = unboxed.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            var method = targetType.GetMethod(methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, argTypes, null)
                ?? throw new Exception($"[ACTIONS CONFIG] Method '{methodName}' not found on '{typeName}' "
                    + $"with args ({string.Join(", ", argTypes.Select(t => t.Name))}).");

            method.Invoke(null, unboxed);
        }

        private static object? UnboxJsonElement(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt32(out int i) ? i
                                 : el.TryGetSingle(out float f) ? f
                                 : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

        private static string UnboxToString(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString()!,
            _ => el.GetRawText()
        };
    }
}