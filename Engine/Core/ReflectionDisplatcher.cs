using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    public static class ReflectionDispatcher
    {
        private static readonly HashSet<Type> NumericTypes = new()
        {
            typeof(int), typeof(float), typeof(double), typeof(long), typeof(short), typeof(byte), typeof(decimal)
        };

        private static readonly Dictionary<string, (object? target, Type type)> _memberPathCache = new();
        private static readonly Dictionary<(string call, int argCount), MethodInfo?> _methodCache = new();

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
            call = PronounResolver.Resolve(call);

            int lastDot = call.LastIndexOf('.');
            if (lastDot < 0)
                throw new Exception($"[DISPATCHER] Invalid call format '{call}'.");

            string memberPath = call[..lastDot];
            string methodName = call[(lastDot + 1)..];

            // ── cached member path resolution ─────────────────────────────────────
            if (!_memberPathCache.TryGetValue(memberPath, out var resolved))
            {
                resolved = ResolveMemberPath(memberPath);
                _memberPathCache[memberPath] = resolved;
            }
            var (target, targetType) = resolved;

            Type[] argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
            var lookupType = target?.GetType() ?? targetType;
            var bindingFlags = BindingFlags.Public |
                               (target == null ? BindingFlags.Static : BindingFlags.Instance);

            // ── cached method lookup ───────────────────────────────────────────────
            var methodKey = (call, args.Length);
            if (!_methodCache.TryGetValue(methodKey, out var method))
            {
                method = lookupType.GetMethods(bindingFlags)
                    .Where(m => m.Name == methodName)
                    .Where(m => m.GetParameters().Length == args.Length)
                    .FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        for (int i = 0; i < ps.Length; i++)
                        {
                            if (args[i] == null) continue;
                            if (ps[i].ParameterType.IsAssignableFrom(argTypes[i])) continue;
                            if (IsNumericConvertible(ps[i].ParameterType, argTypes[i])) continue;
                            return false;
                        }
                        return true;
                    });
                _methodCache[methodKey] = method;
            }

            if (method == null)
                throw new Exception($"[DISPATCHER] Method '{methodName}' not found on '{lookupType.FullName}' "
                    + $"with args ({string.Join(", ", argTypes.Select(t => t.Name))}).");

            object?[] coercedArgs = CoerceArgs(method, args);

            try
            {
                return method.Invoke(target, coercedArgs);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                // Unwrap so the crash log shows the REAL exception (type, message,
                // original stack trace) instead of the generic reflection wrapper.
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw; // unreachable, satisfies the compiler
            }
            catch (Exception ex)
            {
                // Helpful context: which "call" string actually failed, and with what args.
                throw new Exception(
                    $"[DISPATCHER] Invoke failed for '{call}' with args " +
                    $"({string.Join(", ", coercedArgs.Select(a => a?.ToString() ?? "null"))}).", ex);
            }
        }

        // ── numeric coercion ──────────────────────────────────────────────────────

        /// <summary>
        /// Reflection's GetMethod/Invoke does NOT perform the implicit numeric
        /// widening (float→double, int→float, etc.) that a normal C# call site
        /// gets for free. Without this, a method expecting `double` will fail to
        /// match (and fail to invoke) when called with a boxed `float` argument —
        /// e.g. from $delta (double) vs. an axis value stored as float.
        /// This relaxes BOTH the match check and the actual invoke args so any
        /// numeric type can bind to any other numeric parameter type.
        /// </summary>
        private static bool IsNumericConvertible(Type target, Type source)
            => NumericTypes.Contains(target) && NumericTypes.Contains(source);

        private static object?[] CoerceArgs(MethodInfo method, object?[] args)
        {
            var ps = method.GetParameters();
            var result = new object?[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) { result[i] = null; continue; }

                Type paramType = ps[i].ParameterType;
                Type argType = args[i]!.GetType();

                result[i] = (paramType != argType && NumericTypes.Contains(paramType) && NumericTypes.Contains(argType))
                    ? Convert.ChangeType(args[i], paramType)
                    : args[i];
            }

            return result;
        }

        // ── member path resolution ────────────────────────────────────────────────

        private static (object? target, Type type) ResolveMemberPath(string path)
        {
            string[] segments = path.Split('.');

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