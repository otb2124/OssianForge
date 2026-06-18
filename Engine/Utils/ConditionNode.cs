using System;
using System.Collections.Generic;
using System.Text.Json;
using OssianForge.Engine.Nodes;

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

}