using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Reflection;
using OssianForge.Engine.Resources;

namespace OssianForge.Engine.Ipc
{
    /// <summary>
    /// Link to the Ossian Manager (Tauri). The manager creates a named-pipe SERVER and passes
    /// its name via "--embedded --pipe NAME"; the engine connects as the CLIENT.
    ///
    /// Protocol: newline-delimited JSON.
    ///   manager -> engine : {"id":1,"method":"nodes.tree","params":null}
    ///   engine  -> manager: {"id":1,"result":...}  or  {"id":1,"error":"message"}
    ///   engine  -> manager: {"event":"nodes.changed","data":null}        (no id)
    ///
    /// THREADING: the pipe is read/written on thread-pool tasks, but every request is executed
    /// through NodeManager.Enqueue, i.e. at the start of the next Update on the engine thread.
    /// The node tree is therefore never touched concurrently with the game loop.
    /// </summary>
    public sealed class ManagerLink
    {
        private static string? _pipeName;
        private static ManagerLink? _instance;

        public static bool Embedded { get; private set; }

        /// <summary>Call first thing in Program.cs with the command-line args.</summary>
        public static void Configure(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--embedded") Embedded = true;
                else if (args[i] == "--pipe" && i + 1 < args.Length) _pipeName = args[++i];
            }
        }

        /// <summary>Call from Engine.OnLoad (after Nodes.OnLoad). No-op when launched standalone.</summary>
        public static void StartIfRequested()
        {
            if (string.IsNullOrEmpty(_pipeName) || _instance != null) return;
            _instance = new ManagerLink(_pipeName);
            _instance.Start();
        }

        // ---------------------------------------------------------------------------------

        private static readonly UTF8Encoding Utf8 = new(false);

        private readonly string _pipeNameInstance;
        private readonly Channel<string> _outbox =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        private int _treeDirty;

        private ManagerLink(string pipeName) => _pipeNameInstance = pipeName;

        private void Start()
        {
            Engine.Nodes.NodeManager.TreeChanged += OnTreeChanged;
            _ = Task.Run(RunAsync);
        }

        // ── transport ────────────────────────────────────────────────────────────────────

        private async Task RunAsync()
        {
            try
            {
                // PipeOptions.Asynchronous is REQUIRED: on a synchronous handle a pending ReadFile
                // blocks WriteFile on the same pipe, and the link would deadlock.
                await using var pipe = new NamedPipeClientStream(
                    ".", _pipeNameInstance, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(5000);

                using var reader = new StreamReader(pipe, Utf8);
                await using var writer = new StreamWriter(pipe, Utf8) { AutoFlush = true, NewLine = "\n" };

                var writeLoop = Task.Run(async () =>
                {
                    await foreach (var line in _outbox.Reader.ReadAllAsync())
                        await writer.WriteLineAsync(line);
                });

                string? incoming;
                while ((incoming = await reader.ReadLineAsync()) != null)
                    OnLine(incoming);

                _outbox.Writer.TryComplete();
                await writeLoop;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MANAGER LINK] {ex.Message}");
            }
        }

        private void OnLine(string line)
        {
            long id = -1;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                id = root.GetProperty("id").GetInt64();
                string method = root.GetProperty("method").GetString()!;
                // Clone: the JsonDocument is disposed before the queued action runs.
                JsonElement p = root.TryGetProperty("params", out var pe) ? pe.Clone() : default;

                NodeManager.Enqueue(() => Execute(id, method, p));
            }
            catch (Exception ex)
            {
                Reply(id, error: ex.Message);
            }
        }

        private void Execute(long id, string method, JsonElement p)
        {
            try { Reply(id, result: Dispatch(method, p)); }
            catch (Exception ex) { Reply(id, error: (ex.InnerException ?? ex).Message); }
        }

        private void Reply(long id, JsonNode? result = null, string? error = null)
        {
            var msg = new JsonObject { ["id"] = id };
            if (error != null) msg["error"] = error;
            else msg["result"] = result;
            _outbox.Writer.TryWrite(msg.ToJsonString());
        }

        private void SendEvent(string name, JsonNode? data = null)
        {
            var msg = new JsonObject { ["event"] = name, ["data"] = data };
            _outbox.Writer.TryWrite(msg.ToJsonString());
        }

        // Coalesce: any number of tree edits in a frame produce one event on the next frame.
        private void OnTreeChanged()
        {
            if (Interlocked.Exchange(ref _treeDirty, 1) == 0)
            {
                NodeManager.Enqueue(() =>
                {
                    Interlocked.Exchange(ref _treeDirty, 0);
                    SendEvent("nodes.changed");
                });
            }
        }

        // ── methods (always run on the engine's update thread) ───────────────────────────

        private static JsonNode? Dispatch(string method, JsonElement p)
        {
            var nm = Engine.Nodes.NodeManager;

            switch (method)
            {
                case "ping":
                    return "pong";

                case "nodes.tree":
                    {
                        var roots = new JsonArray();
                        foreach (var root in nm.Roots) roots.Add(TreeNode(root));
                        return roots;
                    }

                case "node.get":
                    return NodeDetail(RequireNode(nm, p));

                case "node.set":
                    {
                        var node = RequireNode(nm, p);
                        string type = Arg(p, "property").GetString()!;
                        string path = Arg(p, "path").GetString()!;

                        var prop = NodeReflection.FindNodeProperty(node, type);
                        if (!node.Writable || !prop.Writable)
                            throw new Exception($"'{node.Id}.{type}' is read-only.");

                        NodeReflection.SetNodePropertyValue(node, type, path, ToValue(Arg(p, "value")));
                        return NodeDetail(node);
                    }

                case "node.enable":
                    {
                        var node = RequireNode(nm, p);
                        node.Enabled = Arg(p, "enabled").GetBoolean();
                        return NodeDetail(node);
                    }

                default:
                    throw new Exception($"Unknown method '{method}'.");
            }
        }

        private static JsonElement Arg(JsonElement p, string key)
        {
            if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(key, out var v))
                throw new ArgumentException($"Missing param '{key}'.");
            return v;
        }

        private static Node RequireNode(NodeManager nm, JsonElement p)
        {
            string id = Arg(p, "id").GetString() ?? throw new ArgumentException("'id' must be a string.");
            return nm.GetNode(id)
                ?? nm.Flatten().FirstOrDefault(n => n.Id == id)
                ?? throw new Exception($"No node with id '{id}'.");
        }

        // JSON array [1,2,3] -> "1,2,3" so it goes through the same TypeCoercer path as scene configs.
        private static object? ToValue(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.Array => string.Join(",", el.EnumerateArray().Select(x => x.ToString())),
            _ => ReflectionDispatcher.UnboxJsonElement(el)
        };

        // ── DTOs ─────────────────────────────────────────────────────────────────────────

        // Light-weight: for the hierarchy panel.
        private static JsonObject TreeNode(Node n)
        {
            var props = new JsonArray();
            foreach (var p in n.Properties) props.Add(p.GetType().Name);

            var children = new JsonArray();
            foreach (var c in n.Children) children.Add(TreeNode(c));

            return new JsonObject
            {
                ["id"] = n.Id,
                ["name"] = n.Name,
                ["enabled"] = n.Enabled,
                ["properties"] = props,
                ["children"] = children,
            };
        }

        // Full: for the inspector. Property members are snapshotted via reflection.
        private static JsonObject NodeDetail(Node n)
        {
            var props = new JsonArray();
            foreach (var p in n.Properties)
            {
                props.Add(new JsonObject
                {
                    ["type"] = p.GetType().Name,
                    ["writable"] = n.Writable && p.Writable,
                    ["values"] = Snapshot.Members(p),
                });
            }

            return new JsonObject
            {
                ["id"] = n.Id,
                ["name"] = n.Name,
                ["enabled"] = n.Enabled,
                ["parent"] = n.Parent?.Id,
                ["properties"] = props,
            };
        }
    }

    /// <summary>
    /// Reflection -> JSON. Public fields and readable properties only, depth-limited, never follows
    /// Node or Resource references (emits "node:ID" / "resource:ID" instead), so it can't loop.
    /// </summary>
    internal static class Snapshot
    {
        private const int MaxDepth = 3;
        private const int MaxItems = 64;

        public static JsonObject Members(object owner, int depth = 0)
        {
            var obj = new JsonObject();
            var type = owner.GetType();

            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                obj[f.Name] = Safe(() => Of(f.GetValue(owner), depth + 1));

            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                obj[p.Name] = Safe(() => Of(p.GetValue(owner), depth + 1));
            }

            return obj;
        }

        private static JsonNode? Of(object? v, int depth)
        {
            switch (v)
            {
                case null: return null;
                case string s: return s;
                case bool b: return b;
                case Enum e: return e.ToString();
                case float f: return float.IsFinite(f) ? JsonValue.Create(f) : JsonValue.Create(f.ToString(CultureInfo.InvariantCulture));
                case double d: return double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(d.ToString(CultureInfo.InvariantCulture));
                case Node n: return $"node:{n.Id}";
                case Resource r: return $"resource:{r.Id}";
                case Delegate: return null;
                case Type t: return t.Name;
            }

            var type = v.GetType();
            if (type.IsPrimitive || v is decimal) return JsonSerializer.SerializeToNode(v, type);
            if (depth >= MaxDepth) return type.Name;

            if (v is System.Collections.IEnumerable seq)
            {
                var arr = new JsonArray();
                int i = 0;
                foreach (var item in seq)
                {
                    if (i++ >= MaxItems) { arr.Add("…"); break; }
                    arr.Add(Of(item, depth + 1));
                }
                return arr;
            }

            return Members(v, depth);
        }

        private static JsonNode? Safe(Func<JsonNode?> read)
        {
            try { return read(); }
            catch (Exception ex) { return $"<{ex.GetType().Name}>"; }
        }
    }
}