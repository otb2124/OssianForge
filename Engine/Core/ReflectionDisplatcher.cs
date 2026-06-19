using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    public static class ReflectionDispatcher
    {
        // ── primary entry points ──────────────────────────────────────────────────

        public static void Invoke(string call, params string[] args)
            => InvokeWithResult(call, args.Select(ParseString).ToArray());

        public static void Invoke(string call, params object?[] args)
            => InvokeWithResult(call, args);

        public static T Invoke<T>(string call, params string[] args)
            => (T)InvokeWithResult(call, args.Select(ParseString).ToArray())!;

        public static T Invoke<T>(string call, params object?[] args)
            => (T)InvokeWithResult(call, args)!;

        public static void InvokeJson(string call, System.Collections.Generic.List<JsonElement> args)
            => InvokeWithResult(call, args.Select(UnboxJsonElement).ToArray()!);

        public static object? InvokeWithResult(string call, object?[] args)
        {
            int lastDot = call.LastIndexOf('.');
            if (lastDot < 0)
                throw new Exception($"[DISPATCHER] Invalid call format '{call}'.");

            string memberPath = call[..lastDot];
            string methodName = call[(lastDot + 1)..];

            var (target, targetType) = ResolveMemberPath(memberPath);

            Type[] argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            var lookupType = target?.GetType() ?? targetType;
            var bindingFlags = BindingFlags.Public |
                                (target == null ? BindingFlags.Static : BindingFlags.Instance);

            var method = lookupType.GetMethods(bindingFlags)
                .Where(m => m.Name == methodName)
                .Where(m => m.GetParameters().Length == args.Length)
                .FirstOrDefault(m =>
                {
                    var ps = m.GetParameters();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (args[i] == null) continue;
                        if (!ps[i].ParameterType.IsAssignableFrom(argTypes[i]))
                            return false;
                    }
                    return true;
                });

            if (method == null)
                throw new Exception($"[DISPATCHER] Method '{methodName}' not found on '{lookupType.FullName}' "
                    + $"with args ({string.Join(", ", argTypes.Select(t => t.Name))}).");

            return method.Invoke(target, args);
        }

        // ── member path resolution ────────────────────────────────────────────────

        /// <summary>
        /// Resolves a dotted path like "OssianForge.Engine.Nodes" by walking left-to-right:
        /// the longest leading segment that matches a real Type is the "root" (static class
        /// or namespace anchor), then each remaining dot-segment is resolved as a static or
        /// instance property/field access on the current target.
        ///
        /// Returns (target, type):
        ///   - If the path resolves to a static class with no further member access,
        ///     target is null and type is that static class — caller treats methodName as static.
        ///   - If the path resolves through a property/field, target is the live instance
        ///     and type is its declared type — caller treats methodName as instance.
        /// </summary>
        private static (object? target, Type type) ResolveMemberPath(string path)
        {
            string[] segments = path.Split('.');

            // Try shortest-to-longest root candidates so member-chain resolution
            // (Engine -> Nodes property) is preferred over deeper direct type matches.
            for (int i = 1; i <= segments.Length; i++)
            {
                string candidate = string.Join('.', segments[..i]);
                var rootType = TryResolveType(candidate);
                if (rootType == null) continue;

                try
                {
                    return WalkMembers(rootType, segments, i);
                }
                catch
                {
                    // This root didn't pan out for the remaining segments — try a longer root.
                    continue;
                }
            }

            throw new Exception($"[DISPATCHER] Could not resolve any type in path '{path}'.");
        }

        private static (object? target, Type type) WalkMembers(Type rootType, string[] segments, int rootEnd)
        {
            object? current = null;
            Type currentType = rootType;

            for (int i = rootEnd; i < segments.Length; i++)
            {
                string member = segments[i];

                var bindingFlags = BindingFlags.Public |
                    (current == null ? BindingFlags.Static : BindingFlags.Instance);

                var prop = currentType.GetProperty(member, bindingFlags);
                if (prop != null)
                {
                    current = prop.GetValue(current);
                    currentType = current?.GetType() ?? prop.PropertyType;
                    continue;
                }

                var field = currentType.GetField(member, bindingFlags);
                if (field != null)
                {
                    current = field.GetValue(current);
                    currentType = current?.GetType() ?? field.FieldType;
                    continue;
                }

                throw new Exception($"Member '{member}' not found on '{currentType.FullName}'.");
            }

            return (current, currentType);
        }

        private static Type? TryResolveType(string candidate)
        {
            var t = Type.GetType(candidate)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(a => a.GetType(candidate))
                       .FirstOrDefault(x => x != null);
            if (t != null) return t;

            string lastSegment = candidate[(candidate.LastIndexOf('.') + 1)..];
            string doubled = $"{candidate}.{lastSegment}";

            return Type.GetType(doubled)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(a => a.GetType(doubled))
                       .FirstOrDefault(x => x != null);
        }

        // ── string parsing ────────────────────────────────────────────────────────

        public static object ParseString(string s)
        {
            if (bool.TryParse(s, out bool b)) return b;
            if (int.TryParse(s, out int i)) return i;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            return s;
        }

        // ── json unboxing ─────────────────────────────────────────────────────────

        public static object? UnboxJsonElement(JsonElement el) => el.ValueKind switch
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

        public static string UnboxJsonElementToString(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString()!,
            _ => el.GetRawText()
        };
    }
}