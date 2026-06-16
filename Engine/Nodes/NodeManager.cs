using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Shaders;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OssianForge.Engine.Nodes
{
    public class NodeManager
    {

        public List<Node> Nodes = new List<Node>();
        private static readonly ConcurrentQueue<Action> _pendingActions = new();

        public NodeManager() { }


        public void Initialize()
        {
            Nodes = new List<Node>();
        }


        public void OnStart()
        {
            foreach (var node in Nodes)
            {
                node.OnStart();
            }
        }

        public void RegisterTree(Node root)
        {
            foreach (var node in Flatten(root))
                Nodes.Add(node);
        }

        private IEnumerable<Node> Flatten(Node node)
        {
            yield return node;
            foreach (var child in node.Children.SelectMany(Flatten))
                yield return child;
        }

        public void UpdateNodes(double delta)
        {
            FlushPendingActions();

            foreach (var node in Nodes)
            {
                node.OnUpdate(delta);
            }
        }

        public void RenderNodes(double delta)
        {
            foreach (var node in Nodes)
            {
                node.OnRender(delta);
            }
        }

        public void AddNode(Node node)
        {
            Nodes.Add(node);
        }

        public void RemoveNode(Node node)
        {
            Nodes.Remove(node);
        }


        public Node GetNode(string id)
        {
            foreach (var node in Nodes)
            {
                var found = FindById(node, id);
                if (found != null) return found;
            }
            return null;
        }

        private Node FindById(Node node, string id)
        {
            if (node.Id == id) return node;
            foreach (var child in node.Children)
            {
                var found = FindById(child, id);
                if (found != null) return found;
            }
            return null;
        }

        public List<Node> GetNodesOfType(Type type)
        {
            var result = new List<Node>();
            foreach (var node in Nodes)
                CollectOfType(node, type, result);
            return result;
        }

        public Node GetNodeOfType(Type type)
        {
            return Nodes.FirstOrDefault(n => n.GetType() == type || n.GetType().IsSubclassOf(type));
        }

        private void CollectOfType(Node node, Type type, List<Node> result)
        {
            if (type.IsInstanceOfType(node)) result.Add(node);
            foreach (var child in node.Children)
                CollectOfType(child, type, result);
        }

        public Node GetNodeWithProperty<T>() where T : NodeProperty
        {
            foreach (var node in Nodes)
            {
                var found = FindNodeWithProperty<T>(node);
                if (found != null) return found;
            }
            return null;
        }

        public List<Node> GetNodesWithProperty<T>() where T : NodeProperty
        {
            var result = new List<Node>();
            foreach (var node in Nodes)
                CollectNodesWithProperty<T>(node, result);
            return result;
        }

        public Node GetNodeWithProperties<T1, T2>()
            where T1 : NodeProperty
            where T2 : NodeProperty
        {
            return GetNodesWithProperties(typeof(T1), typeof(T2)).FirstOrDefault();
        }

        public Node GetNodeWithProperties<T1, T2, T3>()
            where T1 : NodeProperty
            where T2 : NodeProperty
            where T3 : NodeProperty
        {
            return GetNodesWithProperties(typeof(T1), typeof(T2), typeof(T3)).FirstOrDefault();
        }

        public List<Node> GetNodesWithProperties(params Type[] propertyTypes)
        {
            var result = new List<Node>();
            foreach (var node in Nodes)
            {
                if (HasAllProperties(node, propertyTypes))
                    result.Add(node);
            }
            return result;
        }

        private Node FindNodeWithProperty<T>(Node node) where T : NodeProperty
        {
            if (node.GetProperty<T>() != null)
                return node;

            foreach (var child in node.Children)
            {
                var found = FindNodeWithProperty<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void CollectNodesWithProperty<T>(Node node, List<Node> result) where T : NodeProperty
        {
            if (node.GetProperty<T>() != null)
                result.Add(node);

            foreach (var child in node.Children)
                CollectNodesWithProperty<T>(child, result);
        }

        private bool HasAllProperties(Node node, Type[] propertyTypes)
        {
            foreach (var type in propertyTypes)
            {
                if (node.GetProperty(type) == null)
                    return false;
            }
            return true;
        }


        public void FlushPendingActions()
        {
            while (_pendingActions.TryDequeue(out var action))
                action();
        }

        public static void Enqueue(Action action) => _pendingActions.Enqueue(action);


        public List<Node> GetNodesInGroup(string groupId)
        {
            return Nodes
                .Where(n => n.GetProperties<GroupProperty>()
                .Any(g => g.GroupId == groupId))
                .ToList();
        }

        public List<LightData> GetLights()
            => Engine.Nodes.NodeManager
                .GetNodesOfType(typeof(Node))
                .Select(n => new { Node = n, Emission = n.GetProperty<EmissionProperty>() })
                .Where(x => x.Emission != null)
                .Select(x => x.Emission.ToLightData(x.Node))
                .Take(16)
                .ToList();
    }
}
