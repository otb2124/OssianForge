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
            var mesh = GetProperty<MeshProperty>();
            var materials = GetProperties<MaterialProperty>();

            // Draw this node's mesh
            if (transform != null && mesh != null && !mesh.IsBillboard && materials.Count > 0)
                Engine.Graphics.Batch.DrawMesh(mesh, materials, transform);

            // Draw this node's sprite AFTER mesh
            if (mesh != null && mesh.IsBillboard && transform != null)
            {
                var camera = Engine.Graphics.Camera;
                var view = camera.GetView();

                Matrix4x4.Invert(view, out var invView);
                var right = new Vector3(invView.M11, invView.M12, invView.M13);
                var up = new Vector3(invView.M21, invView.M22, invView.M23);
                var forward = new Vector3(invView.M31, invView.M32, invView.M33);

                var billboard = new Matrix4x4(
                    right.X, right.Y, right.Z, 0,
                    up.X, up.Y, up.Z, 0,
                    forward.X, forward.Y, forward.Z, 0,
                    0, 0, 0, 1
                );

                Matrix4x4 model =
                    Matrix4x4.CreateScale(transform.Transform.Scale.X, transform.Transform.Scale.Y, 1.0f) *
                    billboard *
                    Matrix4x4.CreateTranslation(transform.Transform.Position);

                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                gl.DepthMask(false);   // don't write to depth buffer
                gl.Enable(EnableCap.DepthTest);

                Engine.Graphics.Batch.DrawMesh(mesh, materials, model);

                materials[0].ShaderResource.SetVector3("uColor", new Vector3(1f, 1f, 1f));
                materials[0].ShaderResource.SetFloat("uAlpha", 1f);

                // Restore
                gl.DepthMask(true);
                gl.DepthFunc(DepthFunction.Less);
                gl.Disable(EnableCap.Blend);
            }
        }
    }
}