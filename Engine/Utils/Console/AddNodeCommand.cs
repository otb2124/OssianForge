using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;
using OssianForge.Engine.Core;
using System.Reflection;

namespace OssianForge.Engine.Utils.Console
{

    public class AddCommand : ICommand
    {
        public string Name => "add";
        public string Description => "add node name:myNode [parent:parentId] | add property node:nodeId name:TypeName [param:value ...]";

        private const string PropsNamespace = "OssianForge.Engine.Nodes.Props";

        public void Execute(CommandContext ctx)
        {
            // First bare word after "add" is the subcommand: node or property
            var sub = ctx.Args.Keys.FirstOrDefault(k => ctx.Args[k] == "");
            if (sub != "node" && sub != "property")
            {
                DebugConsole.Write("Usage: add node name:myNode | add property node:nodeId name:TypeName", ConsoleColor.Yellow);
                return;
            }

            if (sub == "node") ExecuteAddNode(ctx);
            else ExecuteAddProperty(ctx);
        }

        private void ExecuteAddNode(CommandContext ctx)
        {
            var name = ctx.Get("name");
            if (name == null)
            {
                DebugConsole.Write("Usage: add node name:myNode [parent:parentId]", ConsoleColor.Yellow);
                return;
            }

            var parentId = ctx.Get("parent");
            var parent = parentId != null
                ? Engine.Nodes.NodeManager.GetNode(parentId)
                : Engine.Nodes.NodeManager.GetNode("scene");

            if (parent == null)
            {
                DebugConsole.Write($"Parent '{parentId ?? "scene"}' not found.", ConsoleColor.Red);
                return;
            }

            NodeManager.Enqueue(() =>
            {
                var node = new Node { Name = name, Id = name };
                node.AddProperty(new TransformProperty());
                parent.AddChild(node);
                Engine.Nodes.NodeManager.Nodes.Add(node);
                DebugConsole.Write($"Added node '{name}'", ConsoleColor.Green);
            });
        }

        private void ExecuteAddProperty(CommandContext ctx)
        {
            var id = ctx.Get("node");
            if (id == null)
            {
                DebugConsole.Write("Usage: add property node:nodeId name:TypeName [param:value ...]", ConsoleColor.Yellow);
                return;
            }

            var node = Engine.Nodes.NodeManager.GetNode(id);
            if (node == null)
            {
                DebugConsole.Write($"Node '{id}' not found.", ConsoleColor.Red);
                return;
            }

            var typeName = ctx.Get("name");
            if (typeName == null)
            {
                var available = string.Join(", ", TypeRegistry<NodeProperty>.NamesIn(PropsNamespace));
                DebugConsole.Write($"Specify a property type. Available: {available}", ConsoleColor.Yellow);
                return;
            }

            var propType = TypeRegistry<NodeProperty>.Get(PropsNamespace, typeName);
            if (propType == null)
            {
                var available = string.Join(", ", TypeRegistry<NodeProperty>.NamesIn(PropsNamespace));
                DebugConsole.Write($"Unknown property type '{typeName}'. Available: {available}", ConsoleColor.Red);
                return;
            }

            if (node.GetProperty(propType) != null)
            {
                DebugConsole.Write($"Node '{id}' already has a {typeName}.", ConsoleColor.Yellow);
                return;
            }

            var reservedKeys = new[] { "node", "name", "property" };
            var paramArgs = ctx.Args
                .Where(kv => !reservedKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && kv.Value != "")
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            NodeManager.Enqueue(() =>
            {
                var prop = SetPropertyCommand.TryConstructWithArgs(propType, paramArgs);
                if (prop == null)
                {
                    DebugConsole.Write($"Could not construct {typeName}. Check required params.", ConsoleColor.Red);
                    return;
                }

                // Set any remaining args not consumed by the constructor
                var ctorParamNames = propType.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var (paramName, rawValue) in paramArgs)
                {
                    if (ctorParamNames.Contains(paramName)) continue;
                    if (!SetPropertyCommand.TrySetMember(prop, propType, paramName, rawValue))
                        DebugConsole.Write($"Could not set '{paramName}' on {typeName}.", ConsoleColor.Yellow);
                }

                node.AddProperty(prop);
                DebugConsole.Write($"Added {typeName} to '{id}'", ConsoleColor.Green);
            });
        }
    }

    public class RemoveNodeCommand : ICommand
    {
        public string Name => "remove";
        public string Description => "remove node:nodeId";

        public void Execute(CommandContext ctx)
        {
            var id = ctx.Get("node");
            if (id == null) { DebugConsole.Write("Usage: remove node:nodeId", ConsoleColor.Yellow); return; }

            var node = Engine.Nodes.NodeManager.GetNode(id);
            if (node == null) { DebugConsole.Write($"Node '{id}' not found.", ConsoleColor.Red); return; }

            node.Parent?.RemoveChild(node);
            DebugConsole.Write($"Removed node '{id}'", ConsoleColor.Green);
        }
    }

    public class ListNodesCommand : ICommand
    {
        public string Name => "list";
        public string Description => "list nodes [parent:parentId]";

        public void Execute(CommandContext ctx)
        {
            var parentId = ctx.Get("parent");
            var nodes = parentId != null
                ? Engine.Nodes.NodeManager.GetNode(parentId)?.Children ?? new()
                : Engine.Nodes.NodeManager.Nodes;

            foreach (var node in nodes)
                DebugConsole.Write($"  [{node.Id}] {node.Name}", ConsoleColor.White);

            DebugConsole.Write($"{nodes.Count} node(s)", ConsoleColor.Gray);
        }
    }

    public class SetPropertyCommand : ICommand
    {
        public string Name => "set";
        public string Description => "set node:nodeId property:TypeName [param:value ...]";

        private const string PropsNamespace = "OssianForge.Engine.Nodes.Props";

        public void Execute(CommandContext ctx)
        {
            // -- Resolve node --
            var id = ctx.Get("node");
            if (id == null)
            {
                DebugConsole.Write("Usage: set node:nodeId property:TypeName [param:value]", ConsoleColor.Yellow);
                return;
            }

            var node = Engine.Nodes.NodeManager.GetNode(id);
            if (node == null)
            {
                DebugConsole.Write($"Node '{id}' not found.", ConsoleColor.Red);
                return;
            }

            // -- Resolve property type --
            var typeName = ctx.Get("property");
            if (typeName == null)
            {
                DebugConsole.Write("Specify a property type: property:TypeName", ConsoleColor.Yellow);
                return;
            }

            var propType = TypeRegistry<NodeProperty>.Get(PropsNamespace, typeName);
            if (propType == null)
            {
                var available = string.Join(", ", TypeRegistry<NodeProperty>.NamesIn(PropsNamespace));
                DebugConsole.Write($"Unknown property type '{typeName}'. Available: {available}", ConsoleColor.Red);
                return;
            }

            // -- Get the property instance from the node via reflection --
            var getPropertyMethod = node.GetType()
                .GetMethod("GetProperty",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: Type.EmptyTypes,   // no parameters
                    modifiers: null)
                ?.MakeGenericMethod(propType);

            var nodeProp = getPropertyMethod?.Invoke(node, null);
            if (nodeProp == null)
            {
                DebugConsole.Write($"Node '{id}' has no {typeName}.", ConsoleColor.Red);
                return;
            }

            // -- Apply each remaining arg as param:value --
            var reservedKeys = new[] { "node", "name", "property" };
            var paramArgs = ctx.Args
                .Where(kv => !reservedKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && kv.Value != "")
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            NodeManager.Enqueue(() =>
            {
                var prop = TryConstructWithArgs(propType, paramArgs)
                        ?? (NodeProperty)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(propType);

                if (prop == null)
                {
                    DebugConsole.Write($"Could not construct {typeName}.", ConsoleColor.Red);
                    return;
                }

                // Set any remaining args that weren't consumed by the constructor
                var ctorParamNames = propType.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var (paramName, rawValue) in paramArgs)
                {
                    if (ctorParamNames.Contains(paramName)) continue; // already handled
                    if (!SetPropertyCommand.TrySetMember(prop, propType, paramName, rawValue))
                        DebugConsole.Write($"Could not set '{paramName}' on {typeName}.", ConsoleColor.Yellow);
                }

                node.AddProperty(prop);
                DebugConsole.Write($"Added {typeName} to '{id}'", ConsoleColor.Green);
            });
        }

        // Walks public properties and fields on the instance (and nested objects) by dot-path.
        // e.g. "transform.position" -> nodeProp.Transform.Position
        public static bool TrySetMember(object target, Type type, string path, string rawValue)
        {
            var parts = path.Split('.');
            object current = target;
            Type currentType = type;

            // Walk all but the last segment
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var member = FindMember(currentType, parts[i]);
                if (member == null) return false;
                current = GetMemberValue(member, current);
                currentType = GetMemberType(member);
                if (current == null) return false;
            }

            // Set the final segment
            var finalMember = FindMember(currentType, parts[^1]);
            if (finalMember == null) return false;

            var targetType = GetMemberType(finalMember);
            if (!TryConvert(rawValue, targetType, out var converted)) return false;

            SetMemberValue(finalMember, current, converted);
            return true;
        }

        private static MemberInfo FindMember(Type type, string name) =>
            (MemberInfo)type.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? type.GetField(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        private static Type GetMemberType(MemberInfo m) => m switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => null
        };

        private static object GetMemberValue(MemberInfo m, object target) => m switch
        {
            PropertyInfo p => p.GetValue(target),
            FieldInfo f => f.GetValue(target),
            _ => null
        };

        private static void SetMemberValue(MemberInfo m, object target, object value)
        {
            if (m is PropertyInfo p) p.SetValue(target, value);
            else if (m is FieldInfo f) f.SetValue(target, value);
        }

        internal static NodeProperty TryConstructWithArgs(Type propType, Dictionary<string, string> paramArgs)
        {
            // Try each constructor, best-fit first (most matched params wins)
            var ctors = propType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length);

            foreach (var ctor in ctors)
            {
                var ctorParams = ctor.GetParameters();
                var invokeArgs = new object[ctorParams.Length];
                bool allResolved = true;

                for (int i = 0; i < ctorParams.Length; i++)
                {
                    var p = ctorParams[i];
                    if (paramArgs.TryGetValue(p.Name, out var raw) &&
                        SetPropertyCommand.TryConvert(raw, p.ParameterType, out var converted))
                    {
                        invokeArgs[i] = converted;
                    }
                    else if (p.HasDefaultValue)
                    {
                        invokeArgs[i] = p.DefaultValue;
                    }
                    else
                    {
                        allResolved = false;
                        break;
                    }
                }

                if (!allResolved) continue;

                try { return (NodeProperty)ctor.Invoke(invokeArgs); }
                catch (Exception ex)
                {
                    DebugConsole.Write($"Constructor failed: {ex.InnerException?.Message ?? ex.Message}", ConsoleColor.Red);
                    return null;
                }
            }

            return null;
        }

        private static bool TryConvert(string raw, Type targetType, out object result)
        {
            result = null;
            try
            {
                // Vector3: "x,y,z"
                if (targetType == typeof(Vector3))
                {
                    var parts = raw.Split(',');
                    if (parts.Length != 3) return false;
                    result = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                    return true;
                }

                // Vector2: "x,y"
                if (targetType == typeof(Vector2))
                {
                    var parts = raw.Split(',');
                    if (parts.Length != 2) return false;
                    result = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                    return true;
                }

                // Enum
                if (targetType.IsEnum)
                {
                    result = Enum.Parse(targetType, raw, ignoreCase: true);
                    return true;
                }

                // Primitives + string via Convert
                result = Convert.ChangeType(raw, targetType);
                return true;
            }
            catch { return false; }
        }
    }

    public class HelpCommand : ICommand
    {
        public string Name => "help";
        public string Description => "list all available commands";
        private readonly IReadOnlyDictionary<string, ICommand> _commands;

        public HelpCommand(Dictionary<string, ICommand> commands)
        {
            _commands = commands;
        }

        public void Execute(CommandContext ctx)
        {
            DebugConsole.Write("Available commands:", ConsoleColor.Cyan);
            foreach (var cmd in _commands.Values)
                DebugConsole.Write($"  {cmd.Name,-20} {cmd.Description}", ConsoleColor.White);
        }
    }
}

