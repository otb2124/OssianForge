using System;
using System.Collections.Generic;
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

            var chain = WalkChain(prop, segments);
            var (finalOwner, finalMember, _) = chain[^1];

            Type targetType = GetMemberType(finalMember);
            object parsed = ParseValue(targetType, rawValue);

            SetMember(finalOwner, finalMember, parsed);
            WriteBackChain(chain);
        }

        public static void SetNodePropertyValue(Node node, string propertyTypeName, string memberPath, object? value)
        {
            var property = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            if (property == null) return;

            // 1. Unwrap delegate if necessary
            if (value is Delegate del)
            {
                value = del.DynamicInvoke();
            }

            // 2. Look up property or field at root scope
            PropertyInfo? targetProp = property.GetType().GetProperty(memberPath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? targetField = property.GetType().GetField(memberPath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            Type? targetType = targetProp?.PropertyType ?? targetField?.FieldType;

            // 3. Convert/coerce types if mismatched
            if (value != null && targetType != null && !targetType.IsAssignableFrom(value.GetType()))
            {
                try
                {
                    if (targetType == typeof(Vector3) && value is string strVal)
                    {
                        value = ParseVector3(strVal);
                    }
                    else
                    {
                        value = Convert.ChangeType(value, targetType);
                    }
                }
                catch
                {
                    // Fall back if coercion fails
                }
            }

            // 4. Perform the assignment
            if (targetProp != null && targetProp.CanWrite)
            {
                targetProp.SetValue(property, value);
            }
            else if (targetField != null)
            {
                targetField.SetValue(property, value);
            }
            else
            {
                // Walk nested paths (e.g. "Position.X")
                SetNestedMemberValue(property, memberPath, value);
            }
        }

        private static void SetNestedMemberValue(object target, string memberPath, object? value)
        {
            string[] parts = memberPath.Split('.');
            object current = target;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var type = current.GetType();
                var prop = type.GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null)
                {
                    current = prop.GetValue(current)!;
                    continue;
                }

                var field = type.GetField(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    current = field.GetValue(current)!;
                    continue;
                }

                return;
            }

            string finalMember = parts[^1];
            var finalType = current.GetType();

            var finalProp = finalType.GetProperty(finalMember, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (finalProp != null && finalProp.CanWrite)
            {
                finalProp.SetValue(current, value);
                return;
            }

            var finalField = finalType.GetField(finalMember, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (finalField != null)
            {
                finalField.SetValue(current, value);
            }
        }

        /// <summary>
        /// AddNodePropertyValue(node, "TransformProperty", "Transform.Position", "1,0,0")
        /// Reads the current value, adds the delta component-wise, writes it back.
        /// Works for the same numeric/Vector2/Vector3/Vector4 types SetNodePropertyValue supports.
        /// </summary>
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
        /// Adds a camera-relative directional delta to any Vector3 member on any property type.
        /// The world-space delta is rotated into the node's local space before being applied,
        /// so it works correctly regardless of parent rotation.
        ///
        /// directionSourceNodeId: the id of the node whose TransformProperty.Transform.Rotation.Y
        /// is the camera yaw — e.g. "playerCamera".
        /// </summary>
        public static void AddValueCameraDirection(
            Node node, string propertyTypeName, string memberPath,
            string rawDirection, Node cameraNode, double delta)
        {
            var cameraSelfTransform = cameraNode?.GetProperty<TransformProperty>();
            if (cameraSelfTransform == null) return;

            float yawRad = float.DegreesToRadians(cameraSelfTransform.Transform.Rotation.Y);
            float pitchRad = float.DegreesToRadians(cameraSelfTransform.Transform.Rotation.X);

            float cp = MathF.Cos(pitchRad);
            float sp = MathF.Sin(pitchRad);
            float cy = MathF.Cos(yawRad);
            float sy = MathF.Sin(yawRad);

            // Minecraft-style 3D forward vector:
            // - Uses -sp so looking down correctly moves you down (instead of floating up)
            // - When pitch is 0 (looking straight ahead), Y is 0 so moving forward stays completely flat
            Vector3 forward = new Vector3(sy * cp, -sp, cy * cp);

            // Purely horizontal right vector for clean strafing
            Vector3 right = new Vector3(cy, 0f, -sy);

            // World-aligned vertical axis (like Minecraft creative flight)
            Vector3 up = Vector3.UnitY;

            Vector3 dir = ParseVector3(rawDirection);

            // Combine axes: Forward/backward follows full 3D look direction, strafing is horizontal, vertical is world-up
            Vector3 worldDelta = right * dir.X + up * dir.Y + forward * dir.Z;

            ApplyScaled(node, propertyTypeName, memberPath,
                $"{worldDelta.X.ToString(CultureInfo.InvariantCulture)}," +
                $"{worldDelta.Y.ToString(CultureInfo.InvariantCulture)}," +
                $"{worldDelta.Z.ToString(CultureInfo.InvariantCulture)}",
                delta);
        }

        public static void AddValueCameraDirection(
            Node node, string propertyTypeName, string memberPath,
            string rawDirection, string directionSourceNodeId, double delta)
        {
            var cameraNode = string.Equals(directionSourceNodeId, "$currentCamera", StringComparison.OrdinalIgnoreCase)
                ? Engine.Nodes.NodeManager.GetNodesWithProperty<CameraProperty>()
                    .FirstOrDefault(n => string.Equals(n.Id, Engine.Graphics.CurrentCameraNode, StringComparison.OrdinalIgnoreCase))
                : Engine.Nodes.NodeManager.GetNode(directionSourceNodeId);

            AddValueCameraDirection(node, propertyTypeName, memberPath, rawDirection, cameraNode, delta);
        }

        public static void SetValueCameraDirection(
            Node node, string propertyTypeName, string memberPath,
            string rawDirection, string directionSourceNodeId, double delta)
        {
            var cameraNode = Engine.Nodes.NodeManager.GetNode(directionSourceNodeId);
            var cameraSelfTransform = cameraNode?.GetProperty<TransformProperty>();
            if (cameraSelfTransform == null)
            {
                return;
            }

            float yawRad = float.DegreesToRadians(cameraSelfTransform.Transform.Rotation.Y);
            Vector3 forward = new Vector3(MathF.Sin(yawRad), 0f, MathF.Cos(yawRad));
            Vector3 right = new Vector3(MathF.Cos(yawRad), 0f, -MathF.Sin(yawRad));
            Vector3 dir = ParseVector3(rawDirection);
            Vector3 worldDelta = right * dir.X + Vector3.UnitY * dir.Y + forward * dir.Z;

            var prop = FindNodeProperty(node, propertyTypeName);

            var chain = WalkChain(prop, memberPath.Split('.'));
            var (finalOwner, finalMember, _) = chain[^1];

            SetMember(finalOwner, finalMember, worldDelta);
            WriteBackChain(chain);
        }

        /// <summary>
        /// Sets Transform.Rotation.Y on the target node to match the camera node's yaw + an offset.
        /// targetNode: the node whose rotation to set (e.g. playerBody passed as $child.playerBody)
        /// cameraNodeId: id string of the node holding the camera yaw
        /// yawOffset: 0 = face camera direction, 180 = face opposite
        /// </summary>
        public static void SetYawFromCamera(
            Node targetNode, string propertyTypeName, string memberPath,
            string cameraNodeId, string yawOffsetRaw)
        {
            float yawOffset = float.Parse(yawOffsetRaw, CultureInfo.InvariantCulture);

            var cameraNode = Engine.Nodes.NodeManager.GetNode(cameraNodeId);
            var cameraTransform = cameraNode?.GetProperty<TransformProperty>();
            if (cameraTransform == null) return;

            float cameraYaw = cameraTransform.Transform.Rotation.Y;
            float targetYaw = cameraYaw + yawOffset;
            targetYaw %= 360f;
            if (targetYaw < 0f) targetYaw += 360f;

            var prop = FindNodeProperty(targetNode, propertyTypeName);
            string[] segments = memberPath.Split('.');
            var chain = WalkChain(prop, segments);
            var (finalOwner, finalMember, _) = chain[^1];
            var current = (Vector3)GetMember(finalOwner, finalMember)!;

            SetMember(finalOwner, finalMember, new Vector3(current.X, targetYaw, current.Z));
            WriteBackChain(chain);
        }

        public static void AddNodeProperty(Node node, string propertyName, params string[] args)
        {
            Type propType = FindPropertyType(propertyName);
            var constructors = propType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            var ctor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor == null) return;

            var parameters = ctor.GetParameters();
            object[] coercedArgs = new object[parameters.Length];

            int positionalArgIndex = 0;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i].ParameterType;

                if (paramType == typeof(Dictionary<string, List<string>>))
                {
                    coercedArgs[i] = ParseActionMap(args, positionalArgIndex);
                    break;
                }
                else if (positionalArgIndex < args.Length && !args[positionalArgIndex].Contains(":"))
                {
                    coercedArgs[i] = ParseValue(paramType, args[positionalArgIndex]);
                    positionalArgIndex++;
                }
                else if (parameters[i].HasDefaultValue)
                {
                    coercedArgs[i] = parameters[i].DefaultValue;
                }
            }

            NodeProperty propInstance = (NodeProperty)ctor.Invoke(coercedArgs);
            node.AddProperty(propInstance);
            propInstance.OnStart(node);
        }

        public static void AddNodePropertyToAll(string propertyName)
        {
            AddNodePropertyToAll(propertyName, Array.Empty<string>());
        }

        public static void AddNodePropertyToAll(string propertyName, string arg1)
        {
            AddNodePropertyToAll(propertyName, new[] { arg1 });
        }

        public static void AddNodePropertyToAll(string propertyName, string arg1, string arg2)
        {
            AddNodePropertyToAll(propertyName, new[] { arg1, arg2 });
        }

        public static void AddNodePropertyToAll(string propertyName, string arg1, string arg2, string arg3)
        {
            AddNodePropertyToAll(propertyName, new[] { arg1, arg2, arg3 });
        }

        public static void AddNodePropertyToAll(string propertyName, params string[] args)
        {
            var nodes = Engine.Nodes.NodeManager.GetAllNodesFlat();

            foreach (Node node in nodes)
            {
                if (node != null)
                {
                    AddNodeProperty(node, propertyName, args);
                }
            }
        }

        public static void SetNodeWritable(Node node, bool value)
        {
            node.Writable = value;
        }

        public static void SetNodePropertyWritable(Node node, string nodePropertyId, bool value)
        {
            NodeProperty? prop = null;
            try
            {
                prop = FindNodeProperty(node, nodePropertyId);
            }
            catch
            {
                prop = node.Properties.FirstOrDefault(p =>
                    (p.GetType().GetProperty("Id")?.GetValue(p)?.ToString()?.Equals(nodePropertyId, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.GetType().GetProperty("Name")?.GetValue(p)?.ToString()?.Equals(nodePropertyId, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (prop == null)
            {
                throw new Exception($"[NODE REFLECTION] Node '{node.Id}' has no property matching '{nodePropertyId}'.");
            }

            prop.Writable = value;
        }

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName)
            => InvokePropertyMethod(node, propertyTypeName, methodName, Array.Empty<object?>());

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1 });

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1, object? arg2)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1, arg2 });

        public static object? CallPropertyMethod(Node node, string propertyTypeName, string methodName, object? arg1, object? arg2, object? arg3)
            => InvokePropertyMethod(node, propertyTypeName, methodName, new[] { arg1, arg2, arg3 });

        private static object? CoerceValue(object? value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;

            if (targetType == typeof(Vector3))
            {
                if (value is string sVal)
                {
                    return ParseVector3(sVal);
                }
            }
            else if (targetType == typeof(string))
            {
                if (value is Vector3 vVal)
                {
                    return $"{vVal.X.ToString(CultureInfo.InvariantCulture)},{vVal.Y.ToString(CultureInfo.InvariantCulture)},{vVal.Z.ToString(CultureInfo.InvariantCulture)}";
                }
                return value.ToString();
            }

            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

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
                    else
                    {
                        try
                        {
                            object? coerced = CoerceValue(args[i], ps[i].ParameterType);
                            if (coerced != null && ps[i].ParameterType.IsAssignableFrom(coerced.GetType()))
                            {
                                attemptedArgs[i] = coerced;
                            }
                            else
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        catch
                        {
                            isMatch = false;
                            break;
                        }
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
            var property = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            if (property == null)
                return null;

            if (string.IsNullOrEmpty(memberPath))
                return property;

            return WalkMemberPath(property, memberPath);
        }

        // ── property lookup ────────────────────────────────────────────────────────

        private static NodeProperty FindNodeProperty(Node node, string propertyTypeName)
        {
            var prop = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            return prop ?? throw new Exception(
                $"[NODE REFLECTION] Node '{node.Id}' has no property of type '{propertyTypeName}'.");
        }

        public static bool IsAnimationFinished(Node node, string clipName)
        {
            var anim = node.GetProperty<AnimationProperty>();
            if (anim == null) return false;
            var clip = anim.CurrentClip;
            if (clip == null || clip.Name != clipName) return false;

            return anim.CurrentTime >= clip.DurationTicks - 1.0;
        }

        // ── member path walking ───────────────────────────────────────────────────

        private static List<(object owner, MemberInfo member, bool ownerIsValueType)> WalkChain(object root, string[] segments)
        {
            var chain = new List<(object owner, MemberInfo member, bool ownerIsValueType)>();
            object current = root;

            for (int i = 0; i < segments.Length; i++)
            {
                Type currentType = current.GetType();
                var member = GetFieldOrProperty(currentType, segments[i]);
                bool isValueType = currentType.IsValueType;

                chain.Add((current, member, isValueType));

                if (i < segments.Length - 1)
                {
                    current = GetMember(current, member)!;
                }
            }

            return chain;
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

        private static object? WalkMemberPath(object root, string path)
        {
            object? current = root;
            Type currentType = root.GetType();

            foreach (var segment in path.Split('.'))
            {
                if (current == null) return null;

                var prop = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    current = prop.GetValue(current);
                    currentType = prop.PropertyType;
                    continue;
                }

                var field = currentType.GetField(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    current = field.GetValue(current);
                    currentType = field.FieldType;
                    continue;
                }

                throw new Exception($"Member '{segment}' not found on type '{currentType.FullName}'.");
            }

            return current;
        }

        private static Dictionary<string, List<string>> ParseActionMap(string[] args, int startIndex)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = startIndex; i < args.Length; i++)
            {
                int colonIdx = args[i].IndexOf(':');
                if (colonIdx <= 0) continue;

                string key = args[i][..colonIdx].Trim();
                string val = args[i][(colonIdx + 1)..].Trim();

                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    map[key] = list;
                }
                list.Add(val);
            }

            return map;
        }

        private static void WriteBackChain(List<(object owner, MemberInfo member, bool ownerIsValueType)> chain)
        {
            for (int i = chain.Count - 2; i >= 0; i--)
            {
                var (owner, member, _) = chain[i];
                var (childOwner, _, childWasValueType) = chain[i + 1];
                if (childWasValueType)
                    SetMember(owner, member, childOwner);
            }
        }

        private static Type FindPropertyType(string propertyName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                                    && typeof(NodeProperty).IsAssignableFrom(t));

            return type ?? throw new Exception($"[NODE REFLECTION] Property type '{propertyName}' not found.");
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

            if (targetType.IsEnum)
                return Enum.Parse(targetType, raw, ignoreCase: true);

            throw new Exception($"[NODE REFLECTION] Parsing for type '{targetType.FullName}' is not implemented.");
        }

        private static Vector2 ParseVector2(string raw)
        {
            var parts = raw.Split(',').Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new Vector2(parts[0], parts[1]);
        }

        private static Vector3 ParseVector3(string raw)
        {
            var parts = raw.Split(',').Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(parts[0], parts[1], parts[2]);
        }

        private static Vector4 ParseVector4(string raw)
        {
            var parts = raw.Split(',').Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return new Vector4(parts[0], parts[1], parts[2], parts[3]);
        }
    }
}