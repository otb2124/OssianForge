using OssianForge.Engine.Nodes.Props;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using System.Numerics;
using MaterialProperty = OssianForge.Engine.Nodes.Props.MaterialProperty;

namespace OssianForge.Engine.Nodes
{
    public class Node
    {
        public string Id;
        public string Name;
        public Node Parent;
        public List<Node> Children = new();
        public List<NodeProperty> Properties = new();

        public void AddChild(Node child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public void RemoveChild(Node child)
        {
            child.Parent = null;
            Children.Remove(child);
        }

        public void AddProperty(NodeProperty prop)
        {
            Properties.Add(prop);
        }

        public T GetProperty<T>() where T : NodeProperty
            => Properties.OfType<T>().FirstOrDefault();

        public void SetProperty<T>(T prop) where T : NodeProperty
        {
            var existing = Properties.FindIndex(p => p is T);
            if (existing >= 0)
                Properties[existing] = prop;
            else
                Properties.Add(prop);
        }

        public NodeProperty GetProperty(Type type)
        {
            if (type == null) return null;

            return Properties.FirstOrDefault(p =>
                p.GetType() == type || type.IsAssignableFrom(p.GetType()));
        }

        public List<T> GetProperties<T>() where T : NodeProperty
            => Properties.OfType<T>().ToList();

        public T GetChild<T>() where T : Node
            => Children.OfType<T>().FirstOrDefault();

        public List<T> GetChildren<T>() where T : Node
            => Children.OfType<T>().ToList();












        public virtual void OnUpdate(double delta)
        {
            foreach (var child in Children)
                child.OnUpdate(delta);

            ProcessPropUpdate(delta);
        }

        public virtual void ProcessPropUpdate(double delta)
        {
            var camera = GetProperty<CameraProperty>();

            if (camera != null)
                camera.Camera.OnUpdate(delta);
        }








        public virtual void OnRender(double delta)
        {
            foreach (var child in Children)
                child.OnRender(delta);

            ProcessPropRender(delta);
        }

        public virtual void ProcessPropRender(double delta)
        {
            var transform = GetProperty<TransformProperty>();
            var mesh = GetProperty<MeshProperty>();
            var materials = GetProperties<MaterialProperty>();

            if (transform != null && mesh != null && materials.Count > 0)
                Engine.Graphics.Batch.DrawMesh(mesh, materials, transform);
        }
    }
}