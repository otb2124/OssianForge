using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace OssianForge.Engine.Reflection
{
    /// <summary>
    /// Single source of truth for "can value X become type Y, and if so what is it".
    /// Used by both ReflectionDispatcher (method-arg coercion) and NodeReflection
    /// (property/field member coercion) so the two dispatch systems never again
    /// disagree about what's coercible.
    ///
    /// Context-free by design: no Node/context/delta is threaded through here.
    /// $-token resolution ("$child.foo", "$value.bar", ...) happens upstream in
    /// ArgResolver, before values reach this class. A bare id string ("playerCamera")
    /// is the one case TypeCoercer resolves itself, via NodeManager.GetNode.
    /// </summary>
    public static class TypeCoercer
    {
        public delegate bool Coercer(object? value, Type targetType, out object? result);

        private static readonly List<Coercer> _coercers = new()
        {
            TryAssignable,
            TryNumeric,
            TryStringToBool,
            TryStringToEnum,
            TryStringToVector,
            TryStringToNode,
            TryStringToArray,
            TryConvertChangeType,
        };

        private static readonly HashSet<Type> NumericTypes = new()
        {
            typeof(int), typeof(float), typeof(double), typeof(long),
            typeof(short), typeof(byte), typeof(decimal), typeof(sbyte),
            typeof(ushort), typeof(uint), typeof(ulong)
        };

        /// <summary>
        /// Register an additional coercer, checked after all built-ins fail.
        /// Lets engine code extend coercion (e.g. a new resource-reference type)
        /// without editing this file.
        /// </summary>
        public static void Register(Coercer coercer) => _coercers.Add(coercer);

        public static bool CanCoerce(object? value, Type targetType)
            => TryCoerce(value, targetType, out _);

        public static bool TryCoerce(object? value, Type targetType, out object? result)
        {
            if (value == null)
            {
                result = null;
                // null is only a valid coercion for reference types / Nullable<T>
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            foreach (var coercer in _coercers)
            {
                if (coercer(value, targetType, out result))
                    return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Coerce or throw — for call sites (SetNodePropertyValue, method dispatch)
        /// where a failed coercion is a real error, not a "try the next overload" signal.
        /// </summary>
        public static object? Coerce(object? value, Type targetType)
        {
            if (TryCoerce(value, targetType, out var result))
                return result;

            throw new InvalidCastException(
                $"[TYPE COERCER] Cannot coerce value of type '{value?.GetType().FullName ?? "null"}' to '{targetType.FullName}'.");
        }

        // ── individual coercers, checked in order ───────────────────────────────

        private static bool TryAssignable(object? value, Type targetType, out object? result)
        {
            if (targetType.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }
            result = null;
            return false;
        }

        private static bool TryNumeric(object? value, Type targetType, out object? result)
        {
            result = null;
            if (!NumericTypes.Contains(targetType) || value == null) return false;
            if (!NumericTypes.Contains(value.GetType())) return false;

            try
            {
                result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStringToBool(object? value, Type targetType, out object? result)
        {
            result = null;
            if (targetType != typeof(bool) || value is not string s) return false;
            if (bool.TryParse(s, out bool b)) { result = b; return true; }
            return false;
        }

        private static bool TryStringToEnum(object? value, Type targetType, out object? result)
        {
            result = null;
            if (!targetType.IsEnum || value is not string s) return false;
            try
            {
                result = Enum.Parse(targetType, s, ignoreCase: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStringToVector(object? value, Type targetType, out object? result)
        {
            result = null;
            if (value is not string s) return false;

            if (targetType != typeof(Vector2) && targetType != typeof(Vector3) && targetType != typeof(Vector4))
                return false;

            var parts = s.Split(',');
            int expected = targetType == typeof(Vector2) ? 2 : targetType == typeof(Vector3) ? 3 : 4;
            if (parts.Length != expected) return false;

            var floats = new float[expected];
            for (int i = 0; i < expected; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out floats[i]))
                    return false;
            }

            result = expected switch
            {
                2 => new Vector2(floats[0], floats[1]),
                3 => new Vector3(floats[0], floats[1], floats[2]),
                _ => new Vector4(floats[0], floats[1], floats[2], floats[3]),
            };
            return true;
        }

        /// <summary>
        /// Bare id string -> Node, e.g. "playerCamera" -> NodeManager.GetNode("playerCamera").
        /// Does NOT parse $-tokens: by the time a value reaches TypeCoercer, ArgResolver
        /// should already have resolved any "$..." string. A "$"-prefixed string here is
        /// treated as "not a valid id" and fails, rather than silently misinterpreted.
        /// </summary>
        private static bool TryStringToNode(object? value, Type targetType, out object? result)
        {
            result = null;
            if (!typeof(Nodes.Node).IsAssignableFrom(targetType)) return false;
            if (value is not string s || s.Length == 0 || s[0] == '$') return false;

            // Static facade: Engine.Nodes is the Nodes.Nodes instance field on the
            // Engine static class; .NodeManager is its instance NodeManager.
            var node = Engine.Nodes.NodeManager.GetNode(s);
            if (node == null) return false;

            result = node;
            return true;
        }

        /// <summary>
        /// Comma-separated string -> array, element-wise via TryCoerce (so "1,2,3" -> int[],
        /// "a,b,c" -> string[], etc). Deliberately scalar-element only for now — arrays of
        /// Node references or nested arrays aren't a current use case; extend the element
        /// coercion here if that changes.
        /// </summary>
        private static bool TryStringToArray(object? value, Type targetType, out object? result)
        {
            result = null;
            if (!targetType.IsArray || value is not string s) return false;

            var elementType = targetType.GetElementType()!;
            var parts = s.Split(',');
            var array = Array.CreateInstance(elementType, parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryCoerce(parts[i].Trim(), elementType, out var element))
                    return false;
                array.SetValue(element, i);
            }

            result = array;
            return true;
        }

        private static bool TryConvertChangeType(object? value, Type targetType, out object? result)
        {
            result = null;
            if (value == null || !typeof(IConvertible).IsAssignableFrom(value.GetType())) return false;

            try
            {
                result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}