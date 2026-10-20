using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Scene;
using System.Text.Json;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Config
{
    /// <summary>
    /// Builds one scene instance from JSON. Carries its InstanceScope, so there is no static state: every node
    /// gets LocalId = authored id and Id = scope.IdFor(LocalId). Scene references recurse with a new builder.
    /// </summary>
    internal sealed class SceneBuilder
    {
        private readonly InstanceScope _scope;

        public SceneBuilder(InstanceScope scope) => _scope = scope;

        public static Node Build(JsonElement el, InstanceScope scope = default, Node? parent = null)
            => new SceneBuilder(scope).BuildNode(el, parent);

        public Node BuildNode(JsonElement el, Node? parent)
        {
            var node = new Node();

            if (el.TryGetProperty("id", out var id))
            {
                node.LocalId = id.GetString();
                node.Id = _scope.IdFor(node.LocalId);
            }

            if (el.TryGetProperty("name", out var name))
                node.Name = name.GetString();

            if (el.TryGetProperty("StartEnabled", out var onStart))
                node.StartEnabled = onStart.GetBoolean();

            if (el.TryGetProperty("UpdateEnabled", out var onUpdate))
                node.UpdateEnabled = onUpdate.GetBoolean();

            if (el.TryGetProperty("renderEnabled", out var onRender))
                node.RenderEnabled = onRender.GetBoolean();

            if (el.TryGetProperty("enabled", out var enabled))
                node.Enabled = enabled.GetBoolean();

            if (el.TryGetProperty("properties", out var props))
                foreach (var prop in props.EnumerateArray())
                    node.AddProperty(SceneConfig.ParseProperty(prop));

            var sceneRef = node.GetProperty<SceneReferenceProperty>();
            if (sceneRef != null)
            {
                // A disabled reference stays unresolved.
                if (!node.Enabled)
                    return node;

                return ResolveSceneReference(sceneRef, node, el, parent);
            }

            if (el.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                {
                    var childNode = BuildNode(child, node);
                    childNode.Parent = node;
                    node.AddChild(childNode);
                }

            return node;
        }

        private Node ResolveSceneReference(SceneReferenceProperty sceneRef, Node overrides, JsonElement el, Node? parent)
        {
            var sceneConfig = Engine.Resources.GetResource<SceneConfig>(sceneRef.SceneId)
                ?? throw new Exception($"[SCENE REFERENCE] SceneConfig '{sceneRef.SceneId}' not found.");

            // Everything inside the referenced scene is namespaced under the ref node's global id.
            // A ref without an id is not namespaced (outer scope).
            string refId = overrides.Id;
            bool namespaced = !string.IsNullOrEmpty(refId);
            var innerScope = namespaced ? new InstanceScope(refId) : _scope;

            Node root = sceneConfig.Instantiate(innerScope);

            if (namespaced) root.Id = refId;
            if (!string.IsNullOrEmpty(overrides.LocalId)) root.InstanceName = overrides.LocalId; // how the parent scene sees it
            root.IsInstanceRoot = true;
            root.Parent = parent;

            Node injectTarget = (root.Properties.Count == 0 && root.Children.Count == 1)
                ? root.Children[0]
                : root;

            foreach (var prop in overrides.Properties)
            {
                if (prop is SceneReferenceProperty) continue;

                if (prop.SceneRefTarget == OverrideTarget.Root)
                    root.SetProperty(prop);
                else
                    injectTarget.SetProperty(prop);
            }

            // Ensure root always has a TransformProperty for hierarchy climbing
            if (root.GetProperty<TransformProperty>() == null)
                root.SetProperty(new TransformProperty());

            foreach (var child in root.Children)
                child.Parent = root;

            // Inline children written under the ref node belong to the OUTER scope.
            if (el.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                {
                    var childNode = BuildNode(child, root);
                    root.AddChild(childNode);
                }

            return root;
        }
    }

        /// <summary>
        /// Id namespace of one scene instance. Path is empty for the top-level scene; for a scene reference it is the
        /// reference node's global id; for a runtime spawn it is "&lt;rootLocalId&gt;@&lt;netId&gt;".
        /// </summary>
        public readonly record struct InstanceScope(string Path)
        {
            public string IdFor(string localId) => string.IsNullOrEmpty(Path) ? localId : $"{Path}/{localId}";
        }

    /// <summary>
    /// property type name -> factory taking the property's "data" element (null when absent).
    /// SceneConfig asks this registry first and falls back to its own switch, so property types can move
    /// over one at a time. Packs can register their own types.
    /// </summary>
    public static class PropertyFactory
    {
        private static readonly Dictionary<string, Func<JsonElement?, NodeProperty>> Entries = new(StringComparer.Ordinal);

        static PropertyFactory()
        {
            Register("NetworkIdentityProperty", ParseNetworkIdentity);
            Register("TransformProperty", ParseTransform);
            Register("SceneReferenceProperty", data => new SceneReferenceProperty(SceneConfig.Str(data, 0)));
        }

        public static void Register(string typeName, Func<JsonElement?, NodeProperty> make)
        {
            lock (Entries) Entries[typeName] = make;
        }

        internal static bool TryCreate(string typeName, JsonElement? data, out NodeProperty? prop)
        {
            Func<JsonElement?, NodeProperty>? make;
            lock (Entries) Entries.TryGetValue(typeName, out make);
            prop = make?.Invoke(data);
            return prop != null;
        }

        // "data": [ sendRate ]
        // "data": [ sendRate, [ "SpawnPerPeer", "KeepWhenOwnerLeaves" ] ]
        private static NodeProperty ParseNetworkIdentity(JsonElement? data)
        {
            var p = new NetworkIdentityProperty();
            if (data is { } arr)
            {
                int len = arr.GetArrayLength();
                if (len > 0 && arr[0].ValueKind == JsonValueKind.Number)
                    p.SendRate = arr[0].GetSingle();
                if (len > 1 && arr[1].ValueKind == JsonValueKind.Array)
                {
                    foreach (var flag in arr[1].EnumerateArray())
                        p.Spawn |= Enum.Parse<NetSpawn>(flag.GetString()!, true);
                }
            }
            return p;
        }

        private static NodeProperty ParseTransform(JsonElement? data)
        {
            if (data == null)
            {
                return new TransformProperty();
            }

            var arr = data.Value;

            var vec3arr = arr[0];
            var position = SceneConfig.ParseVector3(vec3arr[0].GetString());
            var rotation = SceneConfig.ParseVector3(vec3arr[1].GetString());
            var scale = SceneConfig.ParseVector3(vec3arr[2].GetString());
            var transform = new Transform(position, rotation, scale);
            var stopProp = PropagationLock.None;

            int len = arr.GetArrayLength();

            RenderSpace space = RenderSpace.World;
            Anchor2D anchor = Anchor2D.None;

            if (len > 1) space = Enum.Parse<RenderSpace>(arr[1].GetString()!, true);
            if (len > 2) anchor = Enum.Parse<Anchor2D>(arr[2].GetString()!, true);
            if (len > 3 && arr[3].ValueKind == JsonValueKind.Array)
            {
                foreach (var flag in arr[3].EnumerateArray())
                    stopProp |= Enum.Parse<PropagationLock>(flag.GetString()!, true);
            }

            return new TransformProperty(transform, space, anchor, stopProp);
        }
    }

}