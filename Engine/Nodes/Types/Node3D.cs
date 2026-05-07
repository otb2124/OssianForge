using Silk.NET.OpenGL;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Utils;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;
using Silk.NET.Assimp;
using MaterialProperty = OssianForge.Engine.Nodes.Props.MaterialProperty;

namespace OssianForge.Engine.Nodes.Types
{
    public class Node3D : Node
    {
        //fix: make this a prop
        public Transform Transform = Transform.Default;

        public Node3D(Transform transform)
        {
            Transform = transform;
        }

        public Node3D()
        {
            Transform = Transform.Default;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        public override void OnRender()
        {
            var skybox = GetProperty<SkyboxProperty>();
            var sprite = GetProperty<SpriteProperty>();
            var mesh = GetProperty<MeshProperty>();
            var materials = GetProperties<MaterialProperty>();

            // 1. SkyboxProperty
            if (skybox != null)
            {
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
            else if(mesh != null)
            {
                int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0 ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;
                foreach (var subMesh in mesh.MeshResource.SubMeshes)
                {
                    int matIndex = subMesh.MaterialIndex - minMatIndex;
                    if (matIndex < 0 || matIndex >= materials.Count) continue;
                    materials[matIndex].Apply(Transform.ToMatrix(), Engine.Graphics.Camera.GetView(), Engine.Graphics.Camera.GetProjection());
                    subMesh.Draw();
                }
            }

            // 3. Opaque children FIRST (no sprites)
            foreach (var child in Children)
            {
                if (child is Node3D n3d && n3d.GetProperty<SpriteProperty>() == null)
                    child.OnRender();
            }

            // 4. Transparent children SECOND
            foreach (var child in Children)
            {
                if (child is Node3D n3d && n3d.GetProperty<SpriteProperty>() != null)
                    child.OnRender();
            }

            // 5. This node's own sprite LAST
            if (sprite != null)
                sprite.Draw(Transform.Position, Transform.Scale);
        }
    }
}
