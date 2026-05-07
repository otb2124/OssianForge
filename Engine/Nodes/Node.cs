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
            var skybox = GetProperty<SkyboxProperty>();
            var sprite = GetProperty<SpriteProperty>();
            var mesh = GetProperty<MeshProperty>();
            var materials = GetProperties<MaterialProperty>();

            // 1. SkyboxProperty
            if (skybox != null)
            {

                //TODO: replace with signal MaterialResource.Cull: bool
                var gl = Engine.Graphics.OpenGL;
                gl.DepthFunc(DepthFunction.Lequal);
                gl.DepthMask(false);

                var mat = materials.FirstOrDefault();

                if (mat != null)
                {
                    mat.ShaderResource.Apply();
                    mat.ShaderResource.SetVector3("uTopColor", skybox.TopColor);
                    mat.ShaderResource.SetVector3("uBottomColor", skybox.BottomColor);
                    var view = Engine.Graphics.Camera.GetView();
                    var viewNoTranslation = new Matrix4x4(
                        view.M11, view.M12, view.M13, 0,
                        view.M21, view.M22, view.M23, 0,
                        view.M31, view.M32, view.M33, 0,
                        0, 0, 0, 1);
                    mat.ShaderResource.SetMatrix4("uView", viewNoTranslation);
                    mat.ShaderResource.SetMatrix4("uProjection", Engine.Graphics.Camera.GetProjection());
                    mat.ShaderResource.SetMatrix4("uModel", Matrix4x4.Identity);
                }

                foreach (var subMesh in mesh.MeshResource.SubMeshes)
                    subMesh.Draw();

                gl.DepthMask(true);
                gl.DepthFunc(DepthFunction.Less);
            }
            // 2. Opaque geometry
            else if (mesh != null)
            {
                int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0 ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;
                foreach (var subMesh in mesh.MeshResource.SubMeshes)
                {
                    int matIndex = subMesh.MaterialIndex - minMatIndex;
                    if (matIndex < 0 || matIndex >= materials.Count) continue;
                    materials[matIndex].Apply(transform.Transform.ToMatrix(), Engine.Graphics.Camera.GetView(), Engine.Graphics.Camera.GetProjection());
                    subMesh.Draw();
                }
            }

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