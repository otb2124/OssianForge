using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Reflection;

namespace OssianForge.Engine.Nodes
{
    /// <summary>
    /// Node-domain glue: finding a NodeProperty on a Node, walking a member path
    /// inside it, and the handful of genuinely node-specific operations (camera-
    /// relative movement, yaw-from-camera, animation-finished check). All generic
    /// path-walking and value-coercion work is delegated to PathResolver and
    /// TypeCoercer so this file no longer duplicates either.
    /// </summary>
    public static class NodeReflection
    {
        // ── public entry points (called via ReflectionDispatcher) ─────────────────

        /// <summary>
        /// SetNodePropertyValue(node, "TransformProperty", "Transform.Position", "10,0,0")
        /// Finds the NodeProperty of the given type name on node, walks the dotted
        /// member path, coerces the value to the member's type, and writes it —
        /// including write-back through any value-type (struct) owners in the chain.
        /// Accepts any value: a raw string ("10,0,0"), an already-typed object
        /// (Vector3, Node, etc.), or a delegate (invoked to get the actual value).
        /// </summary>
        public static void SetNodePropertyValue(Node node, string propertyTypeName, string memberPath, object? value)
        {
            if (value is Delegate del)
                value = del.DynamicInvoke();

            var prop = FindNodeProperty(node, propertyTypeName);
            var chain = PathResolver.Walk(prop, memberPath);
            var targetType = PathResolver.GetMemberType(chain[^1].Member);

            object? coerced = TypeCoercer.Coerce(value, targetType);
            PathResolver.Write(chain, coerced);
        }

        /// <summary>
        /// AddNodePropertyValueScaled(node, "TransformProperty", "Transform.Position", "1,0,0")
        /// Reads the current value, adds the delta component-wise scaled by the
        /// product of any scale factors given, writes it back. Works for the
        /// numeric/Vector2/Vector3/Vector4 types Add/Scale support.
        /// </summary>
        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta)
            => ApplyScaled(node, propertyTypeName, memberPath, rawDelta, 1.0);

        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1)
            => ApplyScaled(node, propertyTypeName, memberPath, rawDelta, scale1);

        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1, double scale2)
            => ApplyScaled(node, propertyTypeName, memberPath, rawDelta, scale1 * scale2);

        public static void AddNodePropertyValueScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double scale1, double scale2, double scale3)
            => ApplyScaled(node, propertyTypeName, memberPath, rawDelta, scale1 * scale2 * scale3);

        /// <summary>
        /// Adds a camera-relative directional delta to any Vector3 member on any property type.
        /// The world-space delta is rotated into the node's local space before being applied,
        /// so it works correctly regardless of parent rotation.
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

            Vector3 dir = (Vector3)TypeCoercer.Coerce(rawDirection, typeof(Vector3))!;

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
            if (cameraSelfTransform == null) return;

            float yawRad = float.DegreesToRadians(cameraSelfTransform.Transform.Rotation.Y);
            Vector3 forward = new Vector3(MathF.Sin(yawRad), 0f, MathF.Cos(yawRad));
            Vector3 right = new Vector3(MathF.Cos(yawRad), 0f, -MathF.Sin(yawRad));
            Vector3 dir = (Vector3)TypeCoercer.Coerce(rawDirection, typeof(Vector3))!;
            Vector3 worldDelta = right * dir.X + Vector3.UnitY * dir.Y + forward * dir.Z;

            var prop = FindNodeProperty(node, propertyTypeName);
            var chain = PathResolver.Walk(prop, memberPath);
            PathResolver.Write(chain, worldDelta);
        }

        /// <summary>
        /// Sets Transform.Rotation.Y on the target node to match the camera node's yaw + an offset.
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
            var chain = PathResolver.Walk(prop, memberPath);
            var current = (Vector3)PathResolver.Read(prop, memberPath)!;

            PathResolver.Write(chain, new Vector3(current.X, targetYaw, current.Z));
        }

        public static void AddNodeProperty(Node node, string propertyName, params string[] args)
        {
            Type propType = FindPropertyType(propertyName);
            var constructors = propType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            var ctor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor == null) return;

            var parameters = ctor.GetParameters();
            object?[] coercedArgs = new object?[parameters.Length];

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
                    coercedArgs[i] = TypeCoercer.Coerce(args[positionalArgIndex], paramType);
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
            => AddNodePropertyToAll(propertyName, Array.Empty<string>());

        public static void AddNodePropertyToAll(string propertyName, string arg1)
            => AddNodePropertyToAll(propertyName, new[] { arg1 });

        public static void AddNodePropertyToAll(string propertyName, string arg1, string arg2)
            => AddNodePropertyToAll(propertyName, new[] { arg1, arg2 });

        public static void AddNodePropertyToAll(string propertyName, string arg1, string arg2, string arg3)
            => AddNodePropertyToAll(propertyName, new[] { arg1, arg2, arg3 });

        public static void AddNodePropertyToAll(string propertyName, params string[] args)
        {
            foreach (Node node in Engine.Nodes.NodeManager.GetAllNodesFlat())
            {
                if (node != null)
                    AddNodeProperty(node, propertyName, args);
            }
        }

        public static void SetNodeWritable(Node node, bool value) => node.Writable = value;

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
                throw new Exception($"[NODE REFLECTION] Node '{node.Id}' has no property matching '{nodePropertyId}'.");

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

        /// <summary>
        /// GetNodePropertyValue(node, "TransformProperty", "Transform.Position")
        /// Mirrors SetNodePropertyValue for reads — usable as a condition leaf's "call".
        /// </summary>
        public static object? GetNodePropertyValue(Node node, string propertyTypeName, string memberPath)
        {
            var property = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            if (property == null) return null;
            if (string.IsNullOrEmpty(memberPath)) return property;

            return PathResolver.Read(property, memberPath);
        }

        public static bool IsAnimationFinished(Node node, string clipName)
        {
            var anim = node.GetProperty<AnimationProperty>();
            if (anim == null) return false;
            var clip = anim.CurrentClip;
            if (clip == null || clip.Name != clipName) return false;

            return anim.CurrentTime >= clip.DurationTicks - 1.0;
        }

        // ── property lookup ────────────────────────────────────────────────────────

        private static NodeProperty FindNodeProperty(Node node, string propertyTypeName)
        {
            var prop = node.Properties.FirstOrDefault(p =>
                p.GetType().Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase));

            return prop ?? throw new Exception(
                $"[NODE REFLECTION] Node '{node.Id}' has no property of type '{propertyTypeName}'.");
        }

        // ── scaled add ─────────────────────────────────────────────────────────────

        private static void ApplyScaled(Node node, string propertyTypeName, string memberPath, string rawDelta, double combined)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            var chain = PathResolver.Walk(prop, memberPath);
            var (finalOwner, finalMember, _) = chain[^1];

            Type valueType = PathResolver.GetMemberType(finalMember);
            object current = PathResolver.GetValue(finalOwner, finalMember)!;
            object parsedDelta = TypeCoercer.Coerce(rawDelta, valueType)!;
            object scaledDelta = Scale(valueType, parsedDelta, combined);
            object result = Add(valueType, current, scaledDelta);

            PathResolver.Write(chain, result);
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

        // ── property-method invocation (cached, mirrors ReflectionDispatcher) ──────

        private record MethodCacheKey(Type PropertyType, string MethodName, string ArgTypeSignature);
        private static readonly ConcurrentDictionary<MethodCacheKey, MethodInfo?> _methodCache = new();

        private static object? InvokePropertyMethod(Node node, string propertyTypeName, string methodName, object?[] args)
        {
            var prop = FindNodeProperty(node, propertyTypeName);
            var propType = prop.GetType();
            string sig = string.Join("_", args.Select(a => a?.GetType().Name ?? "null"));
            var key = new MethodCacheKey(propType, methodName, sig);

            if (!_methodCache.TryGetValue(key, out var method))
            {
                method = FindMatchingOverload(propType, methodName, args);
                _methodCache[key] = method;
            }

            if (method == null)
                throw new Exception(
                    $"[NODE REFLECTION] Method '{methodName}' not found on '{propType.FullName}' " +
                    $"with args ({string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))}).");

            object?[] coercedArgs = CoerceMethodArgs(method, args);

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

        private static MethodInfo? FindMatchingOverload(Type propType, string methodName, object?[] args)
        {
            var overloads = propType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == args.Length);

            foreach (var candidate in overloads)
            {
                var ps = candidate.GetParameters();
                bool isMatch = true;

                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null) continue; // null is checked properly at invoke time via TypeCoercer
                    if (!TypeCoercer.CanCoerce(args[i], ps[i].ParameterType)) { isMatch = false; break; }
                }

                if (isMatch) return candidate;
            }

            return null;
        }

        private static object?[] CoerceMethodArgs(MethodInfo method, object?[] args)
        {
            var ps = method.GetParameters();
            var result = new object?[args.Length];
            for (int i = 0; i < args.Length; i++)
                result[i] = args[i] == null ? null : TypeCoercer.Coerce(args[i], ps[i].ParameterType);
            return result;
        }

        // ── property-type lookup (cached — was an uncached full assembly scan) ─────

        private static readonly ConcurrentDictionary<string, Type> _propertyTypeCache = new(StringComparer.OrdinalIgnoreCase);

        private static Type FindPropertyType(string propertyName)
        {
            if (_propertyTypeCache.TryGetValue(propertyName, out var cached))
                return cached;

            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                                    && typeof(NodeProperty).IsAssignableFrom(t));

            if (type == null)
                throw new Exception($"[NODE REFLECTION] Property type '{propertyName}' not found.");

            _propertyTypeCache[propertyName] = type;
            return type;
        }

        // ── misc parsing ─────────────────────────────────────────────────────────

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
    }
}