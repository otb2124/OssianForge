using OssianForge.Engine.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class ActionRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["call", "args", "storeValue"];
        public string Call { get; set; } = "";
        public string ArgsJson { get; set; } = "[]";
        public string? StoreValue { get; set; } = null;   // key to store return value under, null = discard

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

        public override string GetField(string name) => name switch
        {
            "call" => Call,
            "args" => EncodeArgs(ArgsJson),
            "storeValue" => StoreValue ?? "",
            _ => throw new ArgumentException($"Unknown field '{name}' on ActionRecord")
        };

        public override void SetField(string name, string value)
        {
            switch (name)
            {
                case "call": Call = value; break;
                case "args": ArgsJson = DecodeArgs(value); break;
                case "storeValue": StoreValue = string.IsNullOrEmpty(value) ? null : value; break;
                default: throw new ArgumentException($"Unknown field '{name}' on ActionRecord");
            }
        }

        public static ActionRecord Create(string id, string call, IEnumerable<object?> args, string? storeValue = null)
            => new ActionRecord { Id = id, Call = call, ArgsJson = JsonSerializer.Serialize(args), StoreValue = storeValue };
    }


    // ── config ───────────────────────────────────────────────────────────────────

    public class ActionsConfig : JsonSerialConfig<ActionRecord>
    {
        public ActionsConfig(string id, string path) : base(id, path) { }

        // ── flat-store I/O ────────────────────────────────────────────────────────

        private ActionRecord? ReadActionRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = new ActionRecord
            {
                Id = id,
                Call = GetString($"{prefix}.call"),
                StoreValue = NullIfEmpty(GetString($"{prefix}.storeValue"))
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
            record.ArgsJson = JsonSerializer.Serialize(args);

            return record;
        }

        private void WriteActionRecord(int index, ActionRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);
            Set($"{prefix}.call", record.Call);
            Set($"{prefix}.storeValue", record.StoreValue ?? "");

            var args = record.Args;
            for (int i = 0; i < args.Count; i++)
                Set($"{prefix}.args[{i}]", UnboxToString(args[i]));
        }

        // ── records ───────────────────────────────────────────────────────────────

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

        public List<ActionRecord> GetByCall(string call)
            => GetAllRecords().Where(r => r.Call == call).ToList();

        // ── execution ─────────────────────────────────────────────────────────────

        public void Execute(string id, object context = null, double? delta = null)
        {
            var record = GetById(id)
                ?? throw new Exception($"[ACTIONS CONFIG] Action '{id}' not found.");
            ExecuteRecord(record, context, delta);
        }

        public object? ExecuteWithResult(string id, object context = null, double? delta = null)
        {
            var record = GetById(id)
                ?? throw new Exception($"[ACTIONS CONFIG] Action '{id}' not found.");
            return ExecuteRecord(record, context, delta);
        }

        private object? ExecuteRecord(ActionRecord record, object context, double? delta)
        {
            var args = ResolveArgs(record.Args, context, delta);

            object? result = ReflectionDispatcher.InvokeWithResult(record.Call, args);

            if (record.StoreValue != null)
                ValueStore.Set(record.StoreValue, result);

            return result;
        }

        public void ExecuteAll(IEnumerable<string> ids, object context = null)
        {
            foreach (var id in ids)
                Execute(id, context);
        }


        /// <summary>
        /// Args starting with "$" are treated as value store lookups.
        /// e.g. "$value.myActionOne.result" → ValueStore.Get("value.myActionOne.result")
        /// </summary>
        /// <summary>
        /// Resolves special tokens in args:
        ///   "$self"  → the context object (e.g. calling Node)
        ///   "$delta" → the frame delta (double), if provided
        ///   "$value.key" → ValueStore.Get("value.key")
        /// </summary>
        private static object?[] ResolveArgs(List<JsonElement> args, object context, double? delta)
            => args.Select(el =>
            {
                var unboxed = ReflectionDispatcher.UnboxJsonElement(el);
                if (unboxed is string s)
                {
                    if (s == "$self") return context;
                    if (s == "$delta") return delta ?? 0.0;
                    if (s.StartsWith('$'))
                    {
                        string key = s[1..];
                        var found = ValueStore.Get(key);
                        return found;
                    }
                }

                return unboxed;
            }).ToArray();

        private static string UnboxToString(JsonElement el)
            => ReflectionDispatcher.UnboxJsonElementToString(el);

        private static string? NullIfEmpty(string s)
            => string.IsNullOrEmpty(s) ? null : s;
    }

    // ── value store ───────────────────────────────────────────────────────────────

    public static class ValueStore
    {
        private static readonly Dictionary<string, object?> _values = new();

        public static void Set(string key, object? value) => _values[key] = value;
        public static object? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
        public static bool Has(string key) => _values.ContainsKey(key);
        public static void Clear() => _values.Clear();
    }
}