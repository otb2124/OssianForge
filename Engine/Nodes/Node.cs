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

        public List<T> GetProperties<T>() where T : NodeProperty
            => Properties.OfType<T>().ToList();

        public T GetChild<T>() where T : Node
            => Children.OfType<T>().FirstOrDefault();

        public List<T> GetChildren<T>() where T : Node
            => Children.OfType<T>().ToList();

        public virtual void OnUpdate()
        {
            foreach (var child in Children)
                child.OnUpdate();

            ProcessPropUpdate();
        }

        public virtual void ProcessPropUpdate()
        {
            
        }

        public virtual void OnRender()
        {
            foreach (var child in Children)
                child.OnRender();

            ProcessPropRender();
        }

        public virtual void ProcessPropRender()
        {
            var transform = GetProperty<TransformProperty>();
            var sprite = GetProperty<SpriteProperty>();
            var mesh = GetProperty<MeshProperty>();
            var materials = GetProperties<MaterialProperty>();

            Engine.Graphics.Batch.DrawMesh(mesh, materials, transform);

            // 3. Opaque children FIRST (no sprites)
            foreach (var child in Children)
            {
                if (child is Node n3d && n3d.GetProperty<SpriteProperty>() == null)
                    child.OnRender();
            }

            // 4. Transparent children SECOND
            foreach (var child in Children)
            {
                if (child is Node n3d && n3d.GetProperty<SpriteProperty>() != null)
                    child.OnRender();
            }

            // 5. This node's own sprite LAST
            if (sprite != null)
                sprite.Draw(transform.Transform.Position, transform.Transform.Scale);
        }
    }
}