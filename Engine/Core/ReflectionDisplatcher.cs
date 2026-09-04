using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    public static class ReflectionDispatcher
    {
        private static readonly HashSet<Type> NumericTypes = new()
        {
            typeof(int), typeof(float), typeof(double), typeof(long),
            typeof(short), typeof(byte), typeof(decimal), typeof(sbyte),
            typeof(ushort), typeof(uint), typeof(ulong)
        };

        // Cache resolved member paths
        private static readonly ConcurrentDictionary<string, (object? target, Type type)> _memberPathCache = new();

        // Cached Delegate Invoker to avoid MethodInfo.Invoke performance cost
        private delegate object? FastInvoker(object? target, object?[]? args);

        private record MethodCacheKey(string Call, string ArgTypeSignature);
        private static readonly ConcurrentDictionary<MethodCacheKey, (MethodInfo Method, FastInvoker Invoker)?> _methodCache = new();

        // ── primary entry points ──────────────────────────────────────────────────

        public static void Invoke(string call, params string[] args)
            => InvokeWithResult(call, ParseArgs(args));

        public static void Invoke(string call, params object?[] args)
            => InvokeWithResult(call, args);

        public static T Invoke<T>(string call, params string[] args)
            => (T)InvokeWithResult(call, ParseArgs(args))!;

        public static T Invoke<T>(string call, params object?[] args)
            => (T)InvokeWithResult(call, args)!;

        public static void InvokeJson(string call, List<JsonElement> args)
        {
            object?[] unboxed = new object?[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                unboxed[i] = UnboxJsonElement(args[i]);
            }
            InvokeWithResult(call, unboxed);
        }

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
            var lookupType = target?.GetType() ?? targetType;

            // Generate cache key using types to handle overloads properly
            string sig = GetArgSignature(args);
            var methodKey = new MethodCacheKey(call, sig);

            if (!_methodCache.TryGetValue(methodKey, out var cacheEntry))
            {
                cacheEntry = FindAndCompileMethod(lookupType, target != null, methodName, args);
                _methodCache[methodKey] = cacheEntry;
            }

            if (cacheEntry == null)
                throw new Exception($"[DISPATCHER] Method '{methodName}' not found on '{lookupType.FullName}' with args signature ({sig}).");

            var (method, invoker) = cacheEntry.Value;
            object?[] coercedArgs = CoerceArgs(method, args);

            try
            {
                return invoker(target, coercedArgs);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"[DISPATCHER] Invoke failed for '{call}'.", ex);
            }
        }

        // ── method compilation & resolution ─────────────────────────────────────

        private static (MethodInfo Method, FastInvoker Invoker)? FindAndCompileMethod(Type lookupType, bool isInstance, string methodName, object?[] args)
        {
            var flags = BindingFlags.Public | (isInstance ? BindingFlags.Instance : BindingFlags.Static);
            var methods = lookupType.GetMethods(flags);

            foreach (var m in methods)
            {
                if (m.Name != methodName) continue;

                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;

                bool match = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null) continue;

                    Type argType = args[i]!.GetType();
                    Type paramType = ps[i].ParameterType;

                    if (paramType.IsAssignableFrom(argType)) continue;
                    if (IsNumericConvertible(paramType, argType)) continue;
                    if (CanCoerceFast(paramType, args[i])) continue;

                    match = false;
                    break;
                }

                if (match)
                {
                    return (m, CompileMethod(m));
                }
            }

            return null;
        }

        private static FastInvoker CompileMethod(MethodInfo method)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var argsParam = Expression.Parameter(typeof(object?[]), "args");

            var argExpressions = new List<Expression>();
            var paramInfos = method.GetParameters();

            for (int i = 0; i < paramInfos.Length; i++)
            {
                var argArrayAccess = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                var castArg = Expression.Convert(argArrayAccess, paramInfos[i].ParameterType);
                argExpressions.Add(castArg);
            }

            Expression instance = method.IsStatic ? null! : Expression.Convert(targetParam, method.DeclaringType!);
            Expression callExpr = Expression.Call(instance, method, argExpressions);

            if (method.ReturnType == typeof(void))
            {
                var lambda = Expression.Lambda<Action<object?, object?[]?>>(callExpr, targetParam, argsParam).Compile();
                return (target, args) =>
                {
                    lambda(target, args);
                    return null;
                };
            }
            else
            {
                var castResult = Expression.Convert(callExpr, typeof(object));
                return Expression.Lambda<FastInvoker>(castResult, targetParam, argsParam).Compile();
            }
        }

        // ── fast coercion checks ──────────────────────────────────────────────────

        private static bool CanCoerceFast(Type targetType, object? value)
        {
            if (value == null) return false;

            Type valType = value.GetType();
            if (targetType == typeof(bool))
                return value is bool || (value is string s && bool.TryParse(s, out _));

            if (NumericTypes.Contains(targetType))
            {
                if (NumericTypes.Contains(valType)) return true;
                if (value is string numStr)
                    return double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);
                return false;
            }

            return targetType == typeof(string) || targetType.IsAssignableFrom(valType);
        }

        private static object?[] CoerceArgs(MethodInfo method, object?[] args)
        {
            var ps = method.GetParameters();
            var result = new object?[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) { result[i] = null; continue; }

                Type paramType = ps[i].ParameterType;
                Type argType = args[i]!.GetType();

                if (paramType == argType || paramType.IsAssignableFrom(argType))
                {
                    result[i] = args[i];
                }
                else if (paramType == typeof(bool) && args[i] is string strBool && bool.TryParse(strBool, out bool parsedBool))
                {
                    result[i] = parsedBool;
                }
                else
                {
                    try
                    {
                        result[i] = Convert.ChangeType(args[i], paramType, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // Fallback to original value if conversion fails
                        result[i] = args[i];
                    }
                }
            }

            return result;
        }

        private static bool IsNumericConvertible(Type target, Type source)
            => NumericTypes.Contains(target) && NumericTypes.Contains(source);

        private static string GetArgSignature(object?[] args)
        {
            if (args.Length == 0) return "empty";
            return string.Join("_", args.Select(a => a?.GetType().Name ?? "null"));
        }

        private static object[] ParseArgs(string[] args)
        {
            var result = new object[args.Length];
            for (int i = 0; i < args.Length; i++) result[i] = ParseString(args[i]);
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

        // ── string parsing & unboxing ─────────────────────────────────────────────

        public static object ParseString(string s)
        {
            if (bool.TryParse(s, out bool b)) return b;
            if (int.TryParse(s, out int i)) return i;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            return s;
        }

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
    }
}