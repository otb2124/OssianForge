using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Nodes
{
    public static class NodeReflection
    {
        // ── public entry points (called via ReflectionDispatcher) ─────────────────

        /// <summary>
        /// SetNodePropertyValue(node, "TransformProperty", "Transform.Position", "10,0,0")
        /// Finds the NodeProperty of the given type name on node, walks the dotted
        /// member path, and sets the final field/property to the parsed value.
        /// </summary>
        public static void SetNodePropertyValue(Node node, string propertyTypeName, string memberPath, string rawValue)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            string[] segments = memberPath.Split('.');

            object current = prop;
            var chain = new System.Collections.Generic.List<(object owner, MemberInfo member, bool isValueType)>();

            for (int i = 0; i < segments.Length; i++)
            {
                var member = GetFieldOrProperty(current.GetType(), segments[i]);
                chain.Add((current, member, current.GetType().IsValueType));

                if (i < segments.Length - 1)
                    current = GetMember(current, member);
            }

            // Set the final value
            var (finalOwner, finalMember, _) = chain[^1];
            object parsed = ParseValue(GetMemberType(finalMember), rawValue);
            SetMember(finalOwner, finalMember, parsed);

            // Walk BACKWARD, writing each mutated struct copy back into its parent
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                var (owner, member, _) = chain[i];
                var (childOwner, childMember, childWasValueType) = chain[i + 1];
                if (childWasValueType)
                    SetMember(owner, member, childOwner); // write the mutated struct back up
            }
        }

        /// <summary>
        /// AddNodePropertyValue(node, "TransformProperty", "Transform.Position", "1,0,0")
        /// Reads the current value, adds the delta component-wise, writes it back.
        /// Works for the same numeric/Vector2/Vector3/Vector4 types SetNodePropertyValue supports.
        /// </summary>
        /// 
        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta)
        {
            ApplyScaled(node, propertyTypeName, memberPath, rawDelta, 1.0);
        }
        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1)
        {
            ApplyScaled(node, propertyTypeName, memberPath, rawDelta, scale1);
        }

        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1, double scale2)
        {
            double combined = scale1 * scale2;
            ApplyScaled(node, propertyTypeName, memberPath, rawDelta, combined);
        }

        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1, double scale2, double scale3)
        {
            ApplyScaled(node, propertyTypeName, memberPath, rawDelta, scale1 * scale2 * scale3);
        }


        /// <summary>
        /// Like AddValueCurrentDirection but derives the forward/right axes from a
        /// sibling or child node's Transform.Rotation.Y (camera yaw) rather than
        /// from this node's own world matrix. Used so WASD movement on the player
        /// root is relative to the camera's facing, not the root's facing (which
        /// is always zero in the Skyrim-style hierarchy).
        ///
        /// directionSourceNodeId: the id of the node whose Transform.Rotation.Y
        /// is the camera yaw — e.g. "playerCamera".
        /// </summary>
        public static void AddValueCameraDirection(
            Node node, string propertyTypeName, string memberPath,
            string rawDirection, string directionSourceNodeId, double delta)
        {
            var cameraNode = Engine.Nodes.NodeManager.GetNode(directionSourceNodeId);
            var cameraSelfTransform = cameraNode?.GetProperty<TransformProperty>();
            if (cameraSelfTransform == null) return;

            float yawRad = float.DegreesToRadians(cameraSelfTransform.Transform.Rotation.Y);

            // Build XZ axes from camera yaw only — no pitch, no roll.
            // Forward = into screen at yaw 0; Right = 90° clockwise from forward.
            Vector3 forward = new Vector3(-MathF.Sin(yawRad), 0f, -MathF.Cos(yawRad));
            Vector3 right = new Vector3(-MathF.Cos(yawRad), 0f, MathF.Sin(yawRad));

            Vector3 dir = ParseVector3(rawDirection);

            // dir.X = strafe, dir.Y = vertical (unused for ground movement), dir.Z = forward
            Vector3 rotated = right * dir.X + Vector3.UnitY * dir.Y + forward * dir.Z;

            ApplyScaled(node, propertyTypeName, memberPath,
                $"{rotated.X.ToString(CultureInfo.InvariantCulture)}," +
                $"{rotated.Y.ToString(CultureInfo.InvariantCulture)}," +
                $"{rotated.Z.ToString(CultureInfo.InvariantCulture)}", delta);
        }



        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName)
    => InvokePropertyMethod(node, propertyTypeName, methodName, Array.Empty<object?>());

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1 });

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1, object? arg2)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1, arg2 });

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1, object? arg2, object? arg3)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1, arg2, arg3 });

        private static object? InvokePropertyMethod(Node node, string propertyTypeName, string methodName, object?[] args)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            var overloads = prop.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName)
                .ToList();

            var sameArity = overloads.Where(m => m.GetParameters().Length == args.Length).ToList();

            MethodInfo? method = null;
            object?[]? coercedArgs = null;

            foreach (var candidate in sameArity)
            {
                var ps = candidate.GetParameters();
                bool isMatch = true;
                var attemptedArgs = new object?[args.Length];

                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null)
                    {
                        attemptedArgs[i] = null;
                        continue;
                    }

                    bool assignable = ps[i].ParameterType.IsAssignableFrom(argTypes[i]);

                    if (assignable)
                    {
                        attemptedArgs[i] = args[i];
                    }
                    else if (args[i] is string rawStr)
                    {
                        try
                        {
                            attemptedArgs[i] = ParseValue(ps[i].ParameterType, rawStr);
                        }
                        catch
                        {
                            isMatch = false;
                            break;
                        }
                    }
                    else
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    method = candidate;
                    coercedArgs = attemptedArgs;
                    break;
                }
            }

            if (method == null)
                throw new Exception(
                    $"[NODE REFLECTION] Method '{methodName}' not found on '{prop.GetType().FullName}' " +
                    $"with args ({string.Join(", ", argTypes.Select(t => t?.Name ?? "null"))}).");

            try
            {
                return method.Invoke(prop, coercedArgs);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
        }

        private static void ApplyScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double combined)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            string[] segments = memberPath.Split('.');
            var chain = WalkChain(prop, segments);
            var (finalOwner, finalMember, _) = chain[^1];
            Type valueType = GetMemberType(finalMember);
            object current = GetMember(finalOwner, finalMember)!;
            object parsedDelta = ParseValue(valueType, rawDelta);
            object scaledDelta = Scale(valueType, parsedDelta, combined);
            object result = Add(valueType, current, scaledDelta);
            SetMember(finalOwner, finalMember, result);
            WriteBackChain(chain);
        }


        /// <summary>
        /// GetNodePropertyValue(node, "TransformProperty", "Transform.Position")
        /// Mirrors SetNodePropertyValue for reads — usable as a condition leaf's "call".
        /// </summary>
        public static object? GetNodePropertyValue(Node node, string propertyTypeName, string memberPath)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            var (owner, member) = WalkToFinalMember(prop, memberPath);
            return GetMember(owner, member);
        }

        // ── property lookup ────────────────────────────────────────────────────────

        private static NodeProperty FindNodeProperty(Node node, string propertyTypeName)
        {
            var prop = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            return prop ?? throw new Exception(
                $"[NODE REFLECTION] Node '{node.Id}' has no property of type '{propertyTypeName}'.");
        }

        // ── member path walking ───────────────────────────────────────────────────

        /// <summary>
        /// Walks a dotted path like "Transform.Position" starting from `root`,
        /// returning the (owner object, final MemberInfo) so the caller can both
        /// get and set the final field/property.
        ///
        /// Each intermediate segment is resolved as a field or property and its
        /// VALUE becomes the owner for the next segment. The final segment is
        /// NOT evaluated — its MemberInfo + owning object are returned instead,
        /// so structs (like Vector3) can be set without boxing/copy issues.
        /// </summary>
        private static (object owner, MemberInfo member) WalkToFinalMember(object root, string memberPath)
        {
            string[] segments = memberPath.Split('.');
            object current = root;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var member = GetFieldOrProperty(current.GetType(), segments[i]);
                current = GetMember(current, member);
            }

            var finalMember = GetFieldOrProperty(current.GetType(), segments[^1]);
            return (current, finalMember);
        }

        private static MemberInfo GetFieldOrProperty(Type type, string name)
        {
            MemberInfo member = type.GetField(name, BindingFlags.Public | BindingFlags.Instance)
                ?? (MemberInfo)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

            return member ?? throw new Exception(
                $"[NODE REFLECTION] Member '{name}' not found on '{type.FullName}'.");
        }

        private static Type GetMemberType(MemberInfo member) => member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => throw new Exception("[NODE REFLECTION] Unsupported member type.")
        };

        private static object? GetMember(object owner, MemberInfo member) => member switch
        {
            FieldInfo f => f.GetValue(owner),
            PropertyInfo p => p.GetValue(owner),
            _ => throw new Exception("[NODE REFLECTION] Unsupported member type.")
        };

        // Structs require special handling: setting a field on a boxed struct copy
        // doesn't propagate back. We detect this and re-assign the parent chain.
        // For the common case here (root is a class like TransformProperty, and we
        // set ONE level deep e.g. "Transform.Position" where Transform is a struct
        // field on a class), this works because `owner` (a Transform struct) is
        // itself the VALUE of a field on the class — so we must write back through
        // the original chain. To keep this simple and correct for your engine's
        // actual shape (TransformProperty.Transform is a field of struct type
        // Transform, and Transform.Position is a field of struct type Vector3),
        // SetMember below re-walks and writes back at every struct boundary.
        private static void SetMember(object owner, MemberInfo member, object value)
        {
            switch (member)
            {
                case FieldInfo f: f.SetValue(owner, value); break;
                case PropertyInfo p: p.SetValue(owner, value); break;
                default: throw new Exception("[NODE REFLECTION] Unsupported member type.");
            }
        }

        private static object Add(Type type, object a, object b)
        {
            if (type == typeof(int)) return (int)a + (int)b;
            if (type == typeof(float)) return (float)a + (float)b;
            if (type == typeof(double)) return (double)a + (double)b;
            if (type == typeof(Vector2)) return (Vector2)a + (Vector2)b;
            if (type == typeof(Vector3)) return (Vector3)a + (Vector3)b;
            if (type == typeof(Vector4)) return (Vector4)a + (Vector4)b;

            throw new Exception($"[NODE REFLECTION] Add not supported for type '{type.FullName}'.");
        }

        private static object Scale(Type type, object value, double t)
        {
            if (type == typeof(int)) return (int)((int)value * t);
            if (type == typeof(float)) return (float)((float)value * t);
            if (type == typeof(double)) return (double)value * t;
            if (type == typeof(Vector2)) return (Vector2)value * (float)t;
            if (type == typeof(Vector3)) return (Vector3)value * (float)t;
            if (type == typeof(Vector4)) return (Vector4)value * (float)t;

            throw new Exception($"[NODE REFLECTION] Scale not supported for '{type.FullName}'.");
        }

        private static System.Collections.Generic.List<(object owner, MemberInfo member, bool ownerIsValueType)> WalkChain(object root, string[] segments)
        {
            var chain = new System.Collections.Generic.List<(object, MemberInfo, bool)>();
            object current = root;

            for (int i = 0; i < segments.Length; i++)
            {
                var member = GetFieldOrProperty(current.GetType(), segments[i]);
                chain.Add((current, member, current.GetType().IsValueType));

                if (i < segments.Length - 1)
                    current = GetMember(current, member)!;
            }

            return chain;
        }

        private static void WriteBackChain(System.Collections.Generic.List<(object owner, MemberInfo member, bool ownerIsValueType)> chain)
        {
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                var (owner, member, _) = chain[i];
                var (childOwner, _, childWasValueType) = chain[i + 1];
                if (childWasValueType)
                    SetMember(owner, member, childOwner);
            }
        }

        // ── value parsing ─────────────────────────────────────────────────────────

        private static object ParseValue(Type targetType, string raw)
        {
            if (targetType == typeof(string)) return raw;
            if (targetType == typeof(bool)) return bool.Parse(raw);
            if (targetType == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);

            if (targetType == typeof(Vector2)) return ParseVector2(raw);
            if (targetType == typeof(Vector3)) return ParseVector3(raw);
            if (targetType == typeof(Vector4)) return ParseVector4(raw);

            if (targetType.IsEnum) return Enum.Parse(targetType, raw, ignoreCase: true);

            throw new Exception($"[NODE REFLECTION] No parser for target type '{targetType.FullName}' (raw='{raw}').");
        }

        private static Vector2 ParseVector2(string s)
        {
            var p = s.Split(',');
            return new Vector2(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture));
        }

        private static Vector3 ParseVector3(string s)
        {
            var p = s.Split(',');
            return new Vector3(
                float.Parse(p[0], CultureInfo.InvariantCulture),
                float.Parse(p[1], CultureInfo.InvariantCulture),
                float.Parse(p[2], CultureInfo.InvariantCulture));
        }

        private static Vector4 ParseVector4(string s)
        {
            var p = s.Split(',');
            return new Vector4(
                float.Parse(p[0], CultureInfo.InvariantCulture),
                float.Parse(p[1], CultureInfo.InvariantCulture),
                float.Parse(p[2], CultureInfo.InvariantCulture),
                float.Parse(p[3], CultureInfo.InvariantCulture));
        }
    }
}