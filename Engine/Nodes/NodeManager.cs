using OssianForge.Engine.Nodes.Props;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;
using LightProperty = OssianForge.Engine.Nodes.Props.EmissionProperty;
using MaterialProperty = OssianForge.Engine.Nodes.Props.MaterialProperty;
using Node = OssianForge.Engine.Nodes.Node;

namespace OssianForge.Engine.Nodes
{
    public class NodeManager
    {

        public List<Node> Nodes = new List<Node>();

        public NodeManager() { }


        public void Initialize()
        {
            Nodes = new List<Node>();
        }

        public void OnLoad()
        {
            Nodes.Add(NodeTree.GetTree());
        }

        public void OnUpdate(double delta)
        {
            foreach (var node in Nodes)
                node.OnUpdate();
        }

        public void OnRender(double delta)
        {
            foreach (var node in Nodes)
                node.OnRender();
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
    }
}
