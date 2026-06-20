using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Utils.ConditionNode
{
    public static class ConditionNodeParser
    {
        /// <summary>
        /// Parses a JSON value into a ConditionNode tree. Accepts:
        ///   { "call": "...", "args": [...], "compare": "greater", "value": 0.1 }   → leaf
        ///   { "and": [ ... ] }
        ///   { "or":  [ ... ] }
        ///   { "not": { ... } }
        /// </summary>
        public static ConditionNode Parse(JsonElement el)
        {
            if (el.TryGetProperty("and", out var andArr))
                return ConditionNode.And(andArr.EnumerateArray().Select(Parse).ToArray());

            if (el.TryGetProperty("or", out var orArr))
                return ConditionNode.Or(orArr.EnumerateArray().Select(Parse).ToArray());

            if (el.TryGetProperty("not", out var notEl))
                return ConditionNode.Not(Parse(notEl));

            // leaf
            string call = el.GetProperty("call").GetString()
                ?? throw new Exception("[CONDITION PARSER] Leaf condition missing 'call'");

            var args = el.TryGetProperty("args", out var argsEl)
                ? argsEl.EnumerateArray().Select(ReflectionDispatcher.UnboxJsonElement).ToArray()
                : Array.Empty<object?>();

            var comparator = el.TryGetProperty("compare", out var cmpEl)
                ? ParseComparator(cmpEl.GetString()!)
                : Comparator.Equals;

            object? expected = el.TryGetProperty("value", out var valEl)
                ? ReflectionDispatcher.UnboxJsonElement(valEl)
                : true; // default: condition.call() == true

            return new LeafConditionNode(call, args, comparator, expected);
        }

        private static Comparator ParseComparator(string s) => s.ToLowerInvariant() switch
        {
            "equals" => Comparator.Equals,
            "notequals" => Comparator.NotEquals,
            "greater" => Comparator.Greater,
            "less" => Comparator.Less,
            "greaterorequal" => Comparator.GreaterOrEqual,
            "lessorequal" => Comparator.LessOrEqual,
            _ => throw new Exception($"[CONDITION PARSER] Unknown comparator '{s}'")
        };
    }
}