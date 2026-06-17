using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Shaders;
using System.Collections.Concurrent;

namespace OssianForge.Engine.Nodes
{
    public class NodeManager
    {
        // Root nodes only. Everything else is reached by walking the tree.
        public List<Node> Roots = new();

        private static readonly ConcurrentQueue<Action> _pendingActions = new();

        public NodeManager() { }

        public void Initialize() => Roots = new();

        // -----------------------------------------------------------------------
        // Tree registration
        // -----------------------------------------------------------------------

        public void RegisterTree(Node root) => Roots.Add(root);

        public void AddNode(Node node) => Roots.Add(node);

        public void RemoveNode(Node node) => Roots.Remove(node);

        // -----------------------------------------------------------------------
        // Lifecycle — recursive tree walks, zero per-frame allocations
        // -----------------------------------------------------------------------

        public void OnStart()
        {
            foreach (var root in Roots)
                StartNode(root);
        }

        public void OnUpdate(double delta)
        {
            FlushPendingActions();
            foreach (var root in Roots)
                UpdateNode(root, delta);
        }

        public void OnRender(double delta)
        {
            foreach (var root in Roots)
                RenderNode(root, delta);
        }

        private static void StartNode(Node node)
        {
            node.OnStart();
            foreach (var child in node.Children)
                StartNode(child);
        }

        private static void UpdateNode(Node node, double delta)
        {
            node.OnUpdate(delta);
            foreach (var child in node.Children)
                UpdateNode(child, delta);
        }

        // Pre-order: parent renders first, children render on top.
        private static void RenderNode(Node node, double delta)
        {
            node.OnRender(delta);
            foreach (var child in node.Children)
                RenderNode(child, delta);
        }

        // -----------------------------------------------------------------------
        // Flat enumeration — on demand, only when queries need it
        // -----------------------------------------------------------------------

        public IEnumerable<Node> Flatten()
        {
            foreach (var root in Roots)
                foreach (var node in Flatten(root))
                    yield return node;
        }

        private static IEnumerable<Node> Flatten(Node node)
        {
            yield return node;
            foreach (var child in node.Children.SelectMany(Flatten))
                yield return child;
        }

        // -----------------------------------------------------------------------
        // Query helpers — enumerate on demand, no stored flat list
        // -----------------------------------------------------------------------

        public Node GetNode(string id)
            => Flatten().FirstOrDefault(n => n.Id == id);

        public List<Node> GetNodesOfType(Type type)
            => Flatten().Where(n => type.IsInstanceOfType(n)).ToList();

        public Node GetNodeOfType(Type type)
            => Flatten().FirstOrDefault(n => n.GetType() == type || n.GetType().IsSubclassOf(type));

        public Node GetNodeWithProperty<T>() where T : NodeProperty
            => Flatten().FirstOrDefault(n => n.GetProperty<T>() != null);

        public List<Node> GetNodesWithProperty<T>() where T : NodeProperty
            => Flatten().Where(n => n.GetProperty<T>() != null).ToList();

        public Node GetNodeWithProperties<T1, T2>()
            where T1 : NodeProperty
            where T2 : NodeProperty
            => Flatten().FirstOrDefault(n =>
                n.GetProperty<T1>() != null &&
                n.GetProperty<T2>() != null);

        public Node GetNodeWithProperties<T1, T2, T3>()
            where T1 : NodeProperty
            where T2 : NodeProperty
            where T3 : NodeProperty
            => Flatten().FirstOrDefault(n =>
                n.GetProperty<T1>() != null &&
                n.GetProperty<T2>() != null &&
                n.GetProperty<T3>() != null);

        public List<Node> GetNodesWithProperties(params Type[] propertyTypes)
            => Flatten().Where(n => propertyTypes.All(t => n.GetProperty(t) != null)).ToList();

        public List<Node> GetNodesInGroup(string groupId)
            => Flatten()
                .Where(n => n.GetProperties<GroupProperty>().Any(g => g.GroupId == groupId))
                .ToList();

        public List<LightData> GetLights()
            => Flatten()
                .Select(n => new { Node = n, Emission = n.GetProperty<EmissionProperty>() })
                .Where(x => x.Emission != null)
                .Select(x => x.Emission.ToLightData(x.Node))
                .Take(16)
                .ToList();

        // -----------------------------------------------------------------------
        // Pending actions
        // -----------------------------------------------------------------------

        public void FlushPendingActions()
        {
            while (_pendingActions.TryDequeue(out var action))
                action();
        }

        public static void Enqueue(Action action) => _pendingActions.Enqueue(action);
    }
}