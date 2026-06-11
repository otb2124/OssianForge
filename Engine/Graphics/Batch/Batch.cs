using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Meshes;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using MaterialProperty = OssianForge.Engine.Nodes.Props.MaterialProperty;

namespace OssianForge.Engine.Graphics.Batch
{
    public class Batch
    {

        public GL OpenGL;

        public Batch()
        {

        }

        public void Init()
        {
            OpenGL = GL.GetApi(Engine.Graphics.Window);
            OpenGL.Enable(EnableCap.DepthTest);
            OpenGL.Disable(EnableCap.CullFace);
            OpenGL.Enable(EnableCap.Blend);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            OpenGL.Enable(EnableCap.DepthTest);
            OpenGL.DepthFunc(DepthFunction.Less);
            OpenGL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
        }


        public void BeginBillbord()
        {
            //OpenGL.Enable(EnableCap.Blend);
            //OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            //OpenGL.DepthMask(false);
            //OpenGL.Enable(EnableCap.DepthTest);
        }

        // Batch.cs
        public void EndBillbord()
        {
            //.DepthMask(true);
            //OpenGL.DepthFunc(DepthFunction.Less);
            //OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            // blend stays ENABLED
        }



        public void DrawMesh(MeshProperty mesh, List<MaterialProperty> materials, TransformProperty transform, AnimationProperty animation)
        {
            if (mesh == null) return;

            int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0
                ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;

            foreach (var subMesh in mesh.MeshResource.SubMeshes)
            {
                int matIndex = subMesh.MaterialIndex - minMatIndex;
                if (matIndex < 0 || matIndex >= materials.Count) continue;

                Matrix4x4[] palette = null;
                if(animation != null)
                {
                    palette = animation.GetPalette(mesh, subMesh);
                }

                DrawSubMesh(subMesh, materials[matIndex], transform.GetMatrix(), palette);
            }
        }

        public void DrawSubMesh(SubMeshResource subMesh, MaterialProperty material, Matrix4x4 matrix, Matrix4x4[] bonePalette)
        {
            material.Apply(matrix, bonePalette);
            subMesh.Draw();
            material.PostApply();
        }

        public void Clear()
        {
            OpenGL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        public void OnResize(Vector2D<int> size)
        {
            OpenGL.Viewport(size);
        }
    }
}
