using System;
using System.Collections.Generic;
using System.Text.Json;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Reflection;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Utils.ConditionNode
{
    // ── condition tree ────────────────────────────────────────────────────────────

    public abstract class ConditionNode
    {
        public abstract bool Evaluate(Node context);

        public static ConditionNode And(params ConditionNode[] children) => new AndConditionNode(children);
        public static ConditionNode Or(params ConditionNode[] children) => new OrConditionNode(children);
        public static ConditionNode Not(ConditionNode child) => new NotConditionNode(child);
    }

    file class AndConditionNode : ConditionNode
    {
        private readonly ConditionNode[] _children;
        public AndConditionNode(ConditionNode[] children) => _children = children;
        public override bool Evaluate(Node context) => Array.TrueForAll(_children, c => c.Evaluate(context));
    }

    file class OrConditionNode : ConditionNode
    {
        private readonly ConditionNode[] _children;
        public OrConditionNode(ConditionNode[] children) => _children = children;
        public override bool Evaluate(Node context) => Array.Exists(_children, c => c.Evaluate(context));
    }

    file class NotConditionNode : ConditionNode
    {
        private readonly ConditionNode _child;
        public NotConditionNode(ConditionNode child) => _child = child;
        public override bool Evaluate(Node context) => !_child.Evaluate(context);
    }

    public enum Comparator
    {
        Equals, NotEquals, Greater, Less, GreaterOrEqual, LessOrEqual
    }

    public class LeafConditionNode : ConditionNode
    {
        private readonly string _call;
        private readonly object?[] _args;
        private readonly Comparator _comparator;
        private readonly object? _expected;

        public LeafConditionNode(string call, object?[] args, Comparator comparator, object? expected)
        {
            _call = call;
            _args = args;
            _comparator = comparator;
            _expected = expected;
        }

        public override bool Evaluate(Node context)
        {
            var resolved = _args.Select(a => ResolveArg(a, context)).ToArray();
            object? actual = ReflectionDispatcher.InvokeWithResult(_call, resolved);
            return Compare(actual, _expected, _comparator);
        }

        // Same token grammar as action args ($self, $child., $group., $id., $value., $currentCamera, bare $key).
        // This used to be a private copy that had drifted: "$value.x" came back null and "$id." was unsupported.
        private static object? ResolveArg(object? arg, Node context)
            => ArgResolver.ResolveValue(arg, context, null);

        private static bool IsNumeric(object? o) =>
            o is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

        private static bool ValuesEqual(object? a, object? b)
        {
            if (IsNumeric(a) && IsNumeric(b))
                return Math.Abs(Convert.ToDouble(a) - Convert.ToDouble(b)) < 1e-9;
            return Equals(a, b);
        }

        private static bool Compare(object? actual, object? expected, Comparator cmp)
        {
            // Equals(1f, 1) is false in C#; a float result must equal an int literal from the config.
            if (cmp == Comparator.Equals) return ValuesEqual(actual, expected);
            if (cmp == Comparator.NotEquals) return !ValuesEqual(actual, expected);

            // numeric comparisons
            double a = Convert.ToDouble(actual);
            double b = Convert.ToDouble(expected);
            return cmp switch
            {
                Comparator.Greater => a > b,
                Comparator.Less => a < b,
                Comparator.GreaterOrEqual => a >= b,
                Comparator.LessOrEqual => a <= b,
                _ => false
            };
        }
    }
}