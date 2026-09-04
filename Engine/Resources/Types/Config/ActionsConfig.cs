using OssianForge.Engine.Core;
using OssianForge.Engine.Nodes;
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
                        string prefix = $"[{i}]";
                        string id = GetString($"{prefix}.id");
                        if (string.IsNullOrEmpty(id)) continue;

                        var record = new ActionRecord
                        {
                            Id = id,
                            Call = GetString($"{prefix}.call"),
                            StoreValue = NullIfEmpty(GetString($"{prefix}.storeValue"))
                        };

                        // Read args individually from flat store — args is a JSON array
                        // flattened as [i].args[0], [i].args[1], etc.
                        var argsList = new List<string>();
                        int j = 0;
                        while (true)
                        {
                            string val = GetString($"{prefix}.args[{j}]");
                            if (string.IsNullOrEmpty(val)) break;
                            argsList.Add(val);
                            j++;
                        }
                        record.ArgsJson = JsonSerializer.Serialize(argsList);

                        _cache[record.Id] = record;
                    }

                    Console.WriteLine($"[ACTIONS CACHE] Done — {_cache.Count} records.");
                }
                return _cache;
            }
        }


        public ActionsConfig(string id, string path) : base(id, path) { }

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
            int j = 0;
            while (true)
            {
                string val = GetString($"{prefix}.args[{j}]");
                if (string.IsNullOrEmpty(val))
                {
                    break;
                }

                // Parse primitive types (bool, int, double) to avoid storing them as pure strings
                argsList.Add(ReflectionDispatcher.ParseString(val));
                j++;
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
                Set($"{prefix}.args[{i}]", UnboxToString(args[i]));
        }

        // ── records ───────────────────────────────────────────────────────────────

        public new List<ActionRecord> GetAllRecords() => Cache.Values.ToList();

        public new ActionRecord? GetById(string id)
        => Cache.TryGetValue(id, out var r) ? r : null;

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

        public void ExecuteAll(IEnumerable<string> ids, object context = null, double? delta = null)
        {
            foreach (var id in ids)
                Execute(id, context, delta);
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
                    if (s.StartsWith("$child."))
                    {
                        string path = s["$child.".Length..];
                        string[] ids = path.Split('.');
                        Node current = context as Node;
                        foreach (string childId in ids)
                        {
                            if (current == null) break;
                            current = current.Children.FirstOrDefault(c => c.Id == childId);
                        }
                        return current;
                    }
                    if (s.StartsWith("$group."))
                    {
                        // $group.groupName.nodeId  or  $group.groupName.0
                        string rest = s["$group.".Length..];
                        int dot = rest.IndexOf('.');
                        if (dot >= 0)
                        {
                            string groupName = rest[..dot];
                            string nodeRef = rest[(dot + 1)..];

                            var group = Engine.Nodes.NodeManager.GetNodesInGroup(groupName);
                            if (group != null)
                            {
                                // Try numeric index first, then id matchs
                                if (int.TryParse(nodeRef, out int idx) && idx >= 0 && idx < group.Count)
                                    return group[idx];

                                return group.FirstOrDefault(n => n.Id == nodeRef);
                            }
                        }
                        return null;
                    }
                    //TODO: fix gap so it doesnt check if found before
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