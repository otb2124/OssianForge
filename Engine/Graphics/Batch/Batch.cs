using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Meshes;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.Math;
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
            OpenGL.Enable(EnableCap.Blend);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            OpenGL.DepthMask(false);
            OpenGL.Enable(EnableCap.DepthTest);
        }

        // Batch.cs
        public void EndBillbord()
        {
            OpenGL.DepthMask(true);
            OpenGL.DepthFunc(DepthFunction.Less);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            // blend stays ENABLED
        }



        public void DrawMesh(MeshProperty mesh, List<MaterialProperty> materials, TransformProperty transform, AnimationProperty animation = null)
        {
            if (mesh == null) return;

            int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0
                ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;

            foreach (var subMesh in mesh.MeshResource.SubMeshes)
            {
                int matIndex = subMesh.MaterialIndex - minMatIndex;
                if (matIndex < 0 || matIndex >= materials.Count) continue;

                Matrix4x4[] palette = null;
                if (animation?.BonePalette != null && animation.BonePalette.Length > 0
                    && mesh.MeshResource.AllBones != null)
                {
                    // Build a palette in THIS submesh's bone order
                    palette = new Matrix4x4[subMesh.Bones.Count];
                    for (int i = 0; i < subMesh.Bones.Count; i++)
                    {
                        int unifiedIdx = mesh.MeshResource.AllBones
                            .FindIndex(b => b.Name == subMesh.Bones[i].Name);
                        palette[i] = unifiedIdx >= 0
                            ? animation.BonePalette[unifiedIdx]
                            : Matrix4x4.Identity;
                    }
                }

                if (mesh.IsBillboard)
                    DrawBillbord(subMesh, materials[matIndex], transform);
                else
                    DrawSubMesh(subMesh, materials[matIndex],
                        transform.Transform.ToMatrix(), palette);
            }
        }

        public void DrawBillbord(SubMeshResource subMesh, MaterialProperty material, TransformProperty transform)
        {
            BeginBillbord();
            DrawSubMesh(subMesh, material, Engine.Graphics.GetCurrentCamera().GetBillboardMatrix(transform.Transform));
            EndBillbord();
        }

        public void DrawSubMesh(SubMeshResource subMesh, MaterialProperty material, Matrix4x4 matrix, Matrix4x4[] bonePalette = null)
        {
            material.Apply(matrix);

            if (bonePalette != null && bonePalette.Length > 0)
                material.ShaderResource.SetBonePalette(bonePalette);
            else
                material.ShaderResource.SetInt("uSkinned", 0);

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
