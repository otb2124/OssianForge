using Silk.NET.OpenGL;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Utils;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;
using Shader = OssianForge.Engine.Nodes.Props.Shader;

namespace OssianForge.Engine.Nodes.Types
{
    public class Node3D : Node
    {

        public Transform Transform = Transform.Default;

        public Node3D(Transform transform)
        {
            Transform = transform;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }

        public override void OnRender()
        {
            var skybox = GetProperty<Skybox>();
            var model = GetProperty<Model>();
            var sprite = GetProperty<Sprite>();

            // 1. Skybox
            if (skybox != null && model != null)
            {
                var gl = Engine.Graphics.OpenGL;
                gl.DepthFunc(DepthFunction.Lequal);
                gl.DepthMask(false);

                var mat = model.Materials.FirstOrDefault();
                if (mat != null)
                {
                    mat.Shader.Use();
                    mat.Shader.SetVector3("uTopColor", skybox.TopColor);
                    mat.Shader.SetVector3("uBottomColor", skybox.BottomColor);
                    var view = Engine.Graphics.Camera.GetView();
                    var viewNoTranslation = new Matrix4x4(
                        view.M11, view.M12, view.M13, 0,
                        view.M21, view.M22, view.M23, 0,
                        view.M31, view.M32, view.M33, 0,
                        0, 0, 0, 1);
                    mat.Shader.SetMatrix4("uView", viewNoTranslation);
                    mat.Shader.SetMatrix4("uProjection", Engine.Graphics.Camera.GetProjection());
                    mat.Shader.SetMatrix4("uModel", Matrix4x4.Identity);
                }

                foreach (var subMesh in model.SubMeshes)
                    subMesh.Draw();

                gl.DepthMask(true);
                gl.DepthFunc(DepthFunction.Less);
            }
            // 2. Opaque geometry
            else if (model != null)
            {
                model.Draw(
                    Transform.ToMatrix(),
                    Engine.Graphics.Camera.GetView(),
                    Engine.Graphics.Camera.GetProjection());
            }

            // 3. Opaque children FIRST (no sprites)
            foreach (var child in Children)
            {
                if (child is Node3D n3d && n3d.GetProperty<Sprite>() == null)
                    child.OnRender();
            }

            // 4. Transparent children SECOND
            foreach (var child in Children)
            {
                if (child is Node3D n3d && n3d.GetProperty<Sprite>() != null)
                    child.OnRender();
            }

            // 5. This node's own sprite LAST
            if (sprite != null)
                sprite.Draw(Transform.Position, Transform.Scale);
        }
    }
}
