using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace OssianForge.Engine.Core
{
    public static class ReflectionDispatcher
    {
        // ── primary entry points ──────────────────────────────────────────────────

        public static void Invoke(string call, params string[] args)
        {
            var (type, method, unboxed) = Resolve(call, args.Select(ParseString).ToArray());
            method.Invoke(null, unboxed);
        }

        public static void Invoke(string call, params object?[] args)
        {
            var (type, method, unboxed) = Resolve(call, args);
            method.Invoke(null, unboxed);
        }

        public static T Invoke<T>(string call, params string[] args)
        {
            var (type, method, unboxed) = Resolve(call, args.Select(ParseString).ToArray());
            return (T)method.Invoke(null, unboxed)!;
        }

        public static T Invoke<T>(string call, params object?[] args)
        {
            var (type, method, unboxed) = Resolve(call, args);
            return (T)method.Invoke(null, unboxed)!;
        }

        public static object? InvokeWithResult(string call, object?[] args)
        {
            var (_, method, unboxed) = Resolve(call, args);
            return method.Invoke(null, unboxed);
        }

        // ── json element overload (preserves ActionsConfig compat) ────────────────

        public static void InvokeJson(string call, System.Collections.Generic.List<JsonElement> args)
            => Invoke(call, args.Select(UnboxJsonElement).ToArray()!);

        // ── core resolution ───────────────────────────────────────────────────────

        private static (Type, MethodInfo, object?[]) Resolve(string call, object?[] args)
        {
            int lastDot = call.LastIndexOf('.');
            if (lastDot < 0)
                throw new Exception($"[DISPATCHER] Invalid call format '{call}'.");

            string typeName = call[..lastDot];
            string methodName = call[(lastDot + 1)..];

            var targetType = Type.GetType(typeName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(a => a.GetType(typeName))
                       .FirstOrDefault(t => t != null)
                ?? throw new Exception($"[DISPATCHER] Type '{typeName}' not found.");

            Type[] argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            var method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Static, null, argTypes, null)
                ?? throw new Exception($"[DISPATCHER] Method '{methodName}' not found on '{typeName}' "
                    + $"with args ({string.Join(", ", argTypes.Select(t => t.Name))}).");

            return (targetType, method, args);
        }

        // ── string parsing ────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a plain string arg into the most specific type:
        /// bool → int → float → string.
        /// </summary>
        public static object ParseString(string s)
        {
            if (bool.TryParse(s, out bool b)) return b;
            if (int.TryParse(s, out int i)) return i;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            return s;
        }

        // ── json unboxing (kept here so ActionsConfig can delegate) ──────────────

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