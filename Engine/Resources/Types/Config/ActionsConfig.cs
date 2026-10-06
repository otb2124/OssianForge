using OssianForge.Engine.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Text.Json;
using OssianForge.Engine.Reflection;

namespace OssianForge.Engine.Resources.Config
{
    // ── record ───────────────────────────────────────────────────────────────────

    public class ActionRecord : ConfigRecord
    {
        public override IEnumerable<string> FieldNames { get; } = ["call", "args", "storeValue"];
        public string Call { get; set; } = "";

        private string _argsJson = "[]";
        private List<JsonElement>? _args;

        public string ArgsJson
        {
            get => _argsJson;
            set { _argsJson = value; _args = null; }   // parsed args are cached until the JSON changes
        }
        public string? StoreValue { get; set; } = null;   // key to store return value under, null = discard

        private static string EncodeArgs(string json)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        private static string DecodeArgs(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return "[]";
            try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
            catch { return "[]"; }
        }

        /// <summary>Parsed once and cached: actions on OnUpdate/OnRender run this every frame.</summary>
        public List<JsonElement> Args
            => _args ??= JsonSerializer.Deserialize<List<JsonElement>>(_argsJson) ?? new();

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

    /// <summary>
    /// JSON-backed storage and record cache for ActionRecords. Token resolution
    /// ("$self", "$child.", etc) lives in ArgResolver; dispatch lives in
    /// ReflectionDispatcher. This class's job ends at "give me the record" and
    /// "run the record" — it doesn't interpret args itself.
    /// </summary>
    public class ActionsConfig : JsonSerialConfig<ActionRecord>
    {
        private Dictionary<string, ActionRecord>? _cache;
        private Dictionary<string, ActionRecord> Cache
        {
            get
            {
                if (_cache == null)
                {
                    _cache = new Dictionary<string, ActionRecord>();
                    int last = GetLastIndex();

                    for (int i = 0; i <= last; i++)
                    {
                        var record = ReadActionRecord($"[{i}]");
                        if (string.IsNullOrEmpty(record.Id)) continue;
                        if (_cache.ContainsKey(record.Id))
                            Console.WriteLine($"[ACTIONS CACHE] Duplicate action id '{record.Id}' in '{Id}': the later entry wins.");
                        _cache[record.Id] = record;
                    }

                    Console.WriteLine($"[ACTIONS CACHE] Done — {_cache.Count} records.");
                }
                return _cache;
            }
        }

        public ActionsConfig(string id, string path) : base(id, path) { }

        // The record cache is built from the flat store, so it must be dropped whenever the store changes.
        public override void Load()
        {
            base.Load();
            _cache = null;
        }

        protected override void OnRecordReplaced(string oldId, ActionRecord newRecord) => _cache = null;
        protected override void OnRecordAdded(ActionRecord record) => _cache = null;
        protected override void OnRecordRemoved(string id) => _cache = null;

        // ── flat-store I/O ────────────────────────────────────────────────────────

        private ActionRecord ReadActionRecord(string prefix)
        {
            var record = new ActionRecord
            {
                Id = GetString($"{prefix}.id"),
                Call = GetString($"{prefix}.call"),
                StoreValue = NullIfEmpty(GetString($"{prefix}.storeValue"))
            };

            var argsList = new List<object>();
            // Walk by key existence, not by value: an empty-string (or null) arg used to end the list
            // and silently drop every argument after it.
            for (int j = 0; HasKey($"{prefix}.args[{j}]"); j++)
            {
                string val = GetString($"{prefix}.args[{j}]");

                // Parse primitive types (bool, int, double) to avoid storing them as pure strings
                argsList.Add(ReflectionDispatcher.ParseString(val));
            }

            record.ArgsJson = JsonSerializer.Serialize(argsList);
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
                Set($"{prefix}.args[{i}]", UnboxJsonElementToString(args[i]));
        }

        // ── records ───────────────────────────────────────────────────────────────

        public new List<ActionRecord> GetAllRecords() => Cache.Values.ToList();

        public new ActionRecord? GetById(string id)
            => Cache.TryGetValue(id, out var r) ? r : null;

        public List<ActionRecord> GetByCall(string call)
            => GetAllRecords().Where(r => r.Call == call).ToList();

        // ── execution ─────────────────────────────────────────────────────────────

        public void Execute(string id, object? context = null, double? delta = null)
        {
            var record = GetById(id)
                ?? throw new Exception($"[ACTIONS CONFIG] Action '{id}' not found.");
            ExecuteRecord(record, context, delta);
        }

        public object? ExecuteWithResult(string id, object? context = null, double? delta = null)
        {
            var record = GetById(id)
                ?? throw new Exception($"[ACTIONS CONFIG] Action '{id}' not found.");
            return ExecuteRecord(record, context, delta);
        }

        public void ExecuteAll(IEnumerable<string> ids, object? context = null, double? delta = null)
        {
            foreach (var id in ids)
                Execute(id, context, delta);
        }

        private object? ExecuteRecord(ActionRecord record, object? context, double? delta)
        {
            object?[] args = ArgResolver.Resolve(record.Args, context, delta);

            object? result;
            try
            {
                result = ReflectionDispatcher.InvokeWithResult(record.Call, args);
            }
            catch (Exception ex)
            {
                // Say which action failed; the dispatcher only knows the call string.
                throw new Exception($"[ACTIONS CONFIG] Action '{record.Id}' ({record.Call}) failed.", ex);
            }

            if (record.StoreValue != null)
                ValueStore.Set(record.StoreValue, result);

            return result;
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string? NullIfEmpty(string s)
            => string.IsNullOrEmpty(s) ? null : s;

        private static string UnboxJsonElementToString(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString()!,
            _ => el.GetRawText()
        };
    }

    // ── value store ───────────────────────────────────────────────────────────────

    public static class ValueStore
    {
        // Written from the input/camera threads (axes) and read from the game thread, so it must be thread-safe.
        private static readonly ConcurrentDictionary<string, object?> _values = new();

        public static void Set(string key, object? value) => _values[key] = value;
        public static object? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
        public static bool Has(string key) => _values.ContainsKey(key);
        public static void Clear() => _values.Clear();
    }
}