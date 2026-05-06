using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Types;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;
using Light = OssianForge.Engine.Nodes.Props.Light;
using Material = OssianForge.Engine.Nodes.Props.Material;
using Node = OssianForge.Engine.Nodes.Types.Node;

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
            var scene = new Node3D(Transform.Default);

            //skybox
            var sky = new Node3D(Transform.Default);
            sky.AddProperty(new Skybox(
                new Vector3(0.4f, 0.6f, 1.0f),
                new Vector3(0.8f, 0.85f, 1.0f)
            ));
            sky.AddProperty(new Model());
            sky.GetProperty<Model>().AddMesh("fastmesh.cube");
            sky.GetProperty<Model>().AddMaterial(new Material(
                "shaderfile.skybox.vert",
                "shaderfile.skybox.frag"
            ));

            

            //light
            var lightNode = new Node3D(new Transform(new Vector3(0f, 5f, 0f), Vector3.Zero, Vector3.One));
            lightNode.AddProperty(Light.White(intensity: 2.0f, radius: 40.0f));
            lightNode.AddProperty(new Sprite("texturefile.light", new Vector2(10, 10)));
            
            //objects
            var house = new Node3D(new Transform(new Vector3(0, 0, -10), Vector3.Zero, Vector3.One));

            house.AddProperty(new Model());
            house.GetProperty<Model>().AddMesh("meshfile.house");
            house.GetProperty<Model>().AddMaterial(new Material("shaderfile.Basic.vert", "shaderfile.Basic.frag", "texturefile.house.barrel"));
            house.GetProperty<Model>().AddMaterial(new Material("shaderfile.Basic.vert", "shaderfile.Basic.frag", "texturefile.brick.d", "texturefile.brick.n"));
            house.GetProperty<Model>().AddMaterial(new Material("shaderfile.Basic.vert", "shaderfile.Basic.frag", "texturefile.house.windows"));
            house.GetProperty<Model>().AddMaterial(new Material("shaderfile.Basic.vert", "shaderfile.Basic.frag", "texturefile.house.wood"));

            var plane = new Node3D(new Transform(new Vector3(0, 0, 0), Vector3.Zero, new Vector3(50, 1, 50)));
            plane.AddProperty(new Model());
            plane.GetProperty<Model>().AddMesh("fastmesh.plane");
            plane.GetProperty<Model>().AddMaterial(new Material("shaderfile.Basic.vert", "shaderfile.Basic.frag", "texturefile.brick.d"));

            scene.AddChild(sky);
            scene.AddChild(lightNode);
            scene.AddChild(house);
            scene.AddChild(plane);

            Nodes.Add(scene);
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
