using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OssianForge.Engine.Reflection
{
    /// <summary>
    /// Single member-path walker for dotted paths like "Transform.Position.X",
    /// shared by ReflectionDispatcher (resolving a call target) and NodeReflection
    /// (resolving/setting a property member). Always builds the full ownership
    /// chain, not just the endpoint — value-type members (structs like Transform
    /// embedded in a class) require writing the mutated copy back into its owner,
    /// and that's only possible if the whole chain was recorded during the walk.
    ///
    /// Field/property segments only — no array/list indexing in a path segment.
    /// Lookup is BindingFlags.Public only, matching both original implementations.
    /// </summary>
    public static class PathResolver
    {
        public readonly record struct PathLink(object Owner, MemberInfo Member, bool OwnerIsValueType);

        /// <summary>
        /// Walk `segments` starting from `root`. Each link's Owner is the object
        /// the member was read from; the last link's member is the path's endpoint.
        /// </summary>
        public static List<PathLink> Walk(object root, string[] segments)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root), "[PATH RESOLVER] Cannot walk a path from a null root.");
            if (segments.Length == 0)
                throw new ArgumentException("[PATH RESOLVER] Path must have at least one segment.", nameof(segments));

            var chain = new List<PathLink>(segments.Length);
            object current = root;

            for (int i = 0; i < segments.Length; i++)
            {
                Type currentType = current.GetType();
                var member = GetFieldOrProperty(currentType, segments[i]);
                chain.Add(new PathLink(current, member, currentType.IsValueType));

                if (i < segments.Length - 1)
                {
                    var next = GetValue(current, member);
                    if (next == null)
                        throw new Exception(
                            $"[PATH RESOLVER] Path segment '{segments[i]}' evaluated to null; cannot continue to '{segments[i + 1]}'.");
                    current = next;
                }
            }

            return chain;
        }

        public static List<PathLink> Walk(object root, string path) => Walk(root, path.Split('.'));

        /// <summary>Read the value at the end of a path.</summary>
        public static object? Read(object root, string path)
        {
            var chain = Walk(root, path);
            var last = chain[^1];
            return GetValue(last.Owner, last.Member);
        }

        public static object? Read(object root, string[] segments)
        {
            var chain = Walk(root, segments);
            var last = chain[^1];
            return GetValue(last.Owner, last.Member);
        }

        /// <summary>
        /// Set the value at the end of a path, then write the mutated value back
        /// up through any value-type owners in the chain so struct copies aren't
        /// silently discarded.
        /// </summary>
        public static void Write(object root, string path, object? value)
            => Write(Walk(root, path), value);

        public static void Write(object root, string[] segments, object? value)
            => Write(Walk(root, segments), value);

        /// <summary>
        /// Set the value at the end of an already-resolved chain (e.g. one you
        /// also used to Read the current value first, for a scaled/additive
        /// update) and write it back through value-type owners.
        /// </summary>
        public static void Write(List<PathLink> chain, object? value)
        {
            var last = chain[^1];
            SetValue(last.Owner, last.Member, value);
            WriteBack(chain);
        }

        public static Type GetMemberType(MemberInfo member) => member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => throw new Exception($"[PATH RESOLVER] Unsupported member type '{member.MemberType}'.")
        };

        public static object? GetValue(object owner, MemberInfo member) => member switch
        {
            FieldInfo f => f.GetValue(owner),
            PropertyInfo p => p.GetValue(owner),
            _ => throw new Exception($"[PATH RESOLVER] Unsupported member type '{member.MemberType}'.")
        };

        public static void SetValue(object owner, MemberInfo member, object? value)
        {
            switch (member)
            {
                case FieldInfo f: f.SetValue(owner, value); break;
                case PropertyInfo p when p.CanWrite: p.SetValue(owner, value); break;
                case PropertyInfo p: throw new Exception($"[PATH RESOLVER] Property '{p.Name}' on '{p.DeclaringType?.FullName}' has no setter.");
                default: throw new Exception($"[PATH RESOLVER] Unsupported member type '{member.MemberType}'.");
            }
        }

        // ── internals ────────────────────────────────────────────────────────────

        /// <summary>
        /// Walk backward through the chain: whenever a link's owner is a value
        /// type, the mutated copy sitting in that link's child slot has to be
        /// written back into the owner (which is itself a member on the link
        /// before it), or the mutation is lost the moment the boxed struct goes
        /// out of scope.
        /// </summary>
        private static void WriteBack(List<PathLink> chain)
        {
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                var owner = chain[i];
                var child = chain[i + 1];
                if (child.OwnerIsValueType)
                    SetValue(owner.Owner, owner.Member, child.Owner);
            }
        }

        private static MemberInfo GetFieldOrProperty(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            MemberInfo? member = type.GetField(name, flags) ?? (MemberInfo?)type.GetProperty(name, flags);

            return member ?? throw new Exception(
                $"[PATH RESOLVER] Member '{name}' not found on '{type.FullName}'.");
        }
    }
}