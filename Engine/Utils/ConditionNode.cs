using System;
using System.Collections.Generic;
using System.Text.Json;
using OssianForge.Engine.Nodes;
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
            // "$self" resolution, same convention as ActionsConfig.ResolveArgs
            var resolved = _args.Select(a => a is string s && s == "$self" ? (object?)context : a).ToArray();
            object? actual = ReflectionDispatcher.InvokeWithResult(_call, resolved);
            return Compare(actual, _expected, _comparator);
        }

        private static bool Compare(object? actual, object? expected, Comparator cmp)
        {
            if (cmp == Comparator.Equals) return Equals(actual, expected);
            if (cmp == Comparator.NotEquals) return !Equals(actual, expected);

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