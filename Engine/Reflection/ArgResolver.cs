using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Reflection;

namespace OssianForge.Engine.Resources.Config
{
    /// <summary>
    /// Resolves "$"-prefixed tokens in action args into real values/objects, using
    /// the action's execution context (typically the calling Node) and frame delta.
    /// Extracted from ActionsConfig.ResolveArgs so the token grammar is its own
    /// testable unit instead of a 90-line lambda glued to JSON deserialization.
    ///
    /// Runs BEFORE TypeCoercer: by the time a value reaches TypeCoercer/PathResolver,
    /// any "$..." string should already have been resolved to a real object here.
    ///
    /// Recognized tokens:
    ///   $self                    -> the context object itself
    ///   $self.properties.T.path  -> NodeReflection.GetNodePropertyValue(context, T, path)
    ///   $delta                   -> the frame delta (double), or 0.0 if none given
    ///   $currentCamera           -> the node with CameraProperty matching Engine.Graphics.CurrentCameraNode
    ///   $child.a.b.c             -> walks context.Children by id, dot-separated
    ///   $group.name.ref          -> NodeManager.GetNodesInGroup(name), indexed by position or id
    ///   $id.nodeId               -> NodeManager.GetNode(nodeId)
    ///   $key (no dot, no other prefix match) -> ValueStore.Get("key")  [back-compat]
    ///
    /// Any other "$prefix.rest" with an unrecognized prefix throws — previously this
    /// silently fell through to ValueStore.Get("prefix.rest"), turning config typos
    /// into quiet nulls instead of visible errors.
    /// </summary>
    public static class ArgResolver
    {
        public delegate object? TokenHandler(string remainder, object? context, double? delta);

        private static readonly Dictionary<string, TokenHandler> _handlers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["self"] = ResolveSelf,
                ["currentCamera"] = (rest, ctx, delta) => ResolveCurrentCamera(),
                ["child"] = (rest, ctx, delta) => ResolveChild(rest, ctx as Node),
                ["group"] = (rest, ctx, delta) => ResolveGroup(rest),
                ["id"] = (rest, ctx, delta) => Engine.Nodes.NodeManager.GetNode(rest),
            };

        /// <summary>
        /// Register an additional "$prefix.rest" token handler. Lets engine code
        /// extend the grammar without editing this file.
        /// </summary>
        public static void Register(string prefix, TokenHandler handler) => _handlers[prefix] = handler;

        public static object?[] Resolve(List<JsonElement> args, object? context, double? delta)
            => args.Select(el => ResolveOne(ReflectionDispatcher.UnboxJsonElement(el), context, delta)).ToArray();

        private static object? ResolveOne(object? unboxed, object? context, double? delta)
        {
            if (unboxed is not string s || s.Length == 0 || s[0] != '$')
                return unboxed;

            // Exact match, no dot: $self, $delta, $currentCamera.
            if (string.Equals(s, "$self", StringComparison.OrdinalIgnoreCase))
                return context;
            if (string.Equals(s, "$delta", StringComparison.OrdinalIgnoreCase))
                return delta ?? 0.0;
            if (string.Equals(s, "$currentCamera", StringComparison.OrdinalIgnoreCase))
                return ResolveCurrentCamera();

            string body = s[1..]; // strip leading '$'
            int dot = body.IndexOf('.');

            // No dot at all: back-compat ValueStore lookup, e.g. "$myActionResult".
            if (dot < 0)
                return ValueStore.Get(body);

            string prefix = body[..dot];
            string rest = body[(dot + 1)..];

            if (_handlers.TryGetValue(prefix, out var handler))
                return handler(rest, context, delta);

            // Special case: $currentCamera can also appear with a trailing dot/segment
            // in some configs (matched via StartsWith in the original); treat bare
            // "$currentCamera..." the same as the exact-prefix form above.
            if (string.Equals(prefix, "currentCamera", StringComparison.OrdinalIgnoreCase))
                return ResolveCurrentCamera();

            if (string.Equals(prefix, "value", StringComparison.OrdinalIgnoreCase))
                return ValueStore.Get(rest);

            throw new Exception(
                $"[ARG RESOLVER] Unrecognized token prefix '${prefix}' in arg '{s}'. " +
                "Expected one of: self, delta, currentCamera, child, group, id, value, or a bare $key.");
        }

        // ── individual token resolvers ───────────────────────────────────────────

        private static object? ResolveSelf(string rest, object? context, double? delta)
        {
            // "$self.properties.TransformProperty.Transform.Position"
            const string propertiesPrefix = "properties.";
            if (!rest.StartsWith(propertiesPrefix, StringComparison.OrdinalIgnoreCase))
                return context; // fallback for "$self.<anything else>", matches original behavior

            string propPath = rest[propertiesPrefix.Length..];
            int dotIndex = propPath.IndexOf('.');

            string propertyTypeName = dotIndex >= 0 ? propPath[..dotIndex] : propPath;
            string memberPath = dotIndex >= 0 ? propPath[(dotIndex + 1)..] : string.Empty;

            if (context is not Node node)
                return null;

            if (string.IsNullOrEmpty(memberPath))
            {
                return node.Properties.FirstOrDefault(p =>
                    p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));
            }

            object? value = NodeReflection.GetNodePropertyValue(node, propertyTypeName, memberPath);
            if (value is Delegate del)
                value = del.DynamicInvoke();

            return value;
        }

        private static Node? ResolveCurrentCamera()
            => Engine.Nodes.NodeManager.GetNodesWithProperty<CameraProperty>()
                .FirstOrDefault(n => string.Equals(n.Id, Engine.Graphics.CurrentCameraNode, StringComparison.OrdinalIgnoreCase));

        private static Node? ResolveChild(string path, Node? context)
        {
            if (context == null) return null;

            Node? current = context;
            foreach (string childId in path.Split('.'))
            {
                if (current == null) break;
                current = current.Children.FirstOrDefault(c => c.Id == childId);
            }
            return current;
        }

        private static object? ResolveGroup(string rest)
        {
            // "$group.groupName.nodeId" or "$group.groupName.0"
            int dot = rest.IndexOf('.');
            if (dot < 0) return null;

            string groupName = rest[..dot];
            string nodeRef = rest[(dot + 1)..];

            var group = Engine.Nodes.NodeManager.GetNodesInGroup(groupName);
            if (group == null) return null;

            if (int.TryParse(nodeRef, out int idx) && idx >= 0 && idx < group.Count)
                return group[idx];

            return group.FirstOrDefault(n => n.Id == nodeRef);
        }
    }
}