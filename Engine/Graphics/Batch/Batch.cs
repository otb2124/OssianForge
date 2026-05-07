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

        public void BeginCull()
        {
            OpenGL.DepthFunc(DepthFunction.Lequal);
            OpenGL.DepthMask(false);
        }

        public void EndCull()
        {
            OpenGL.DepthMask(true);
            OpenGL.DepthFunc(DepthFunction.Less);
        }

        public void BeginBillbord()
        {
            OpenGL.Enable(EnableCap.Blend);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            OpenGL.DepthMask(false);
            OpenGL.Enable(EnableCap.DepthTest);
        }

        public void EndBillbord()
        {
            OpenGL.DepthMask(true);
            OpenGL.DepthFunc(DepthFunction.Less);
            OpenGL.Disable(EnableCap.Blend);
        }

        

        public void DrawMesh(MeshProperty mesh, List<MaterialProperty> materials, TransformProperty transform)
        {
            if (mesh != null)
            {
                int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0 ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;

                foreach (var subMesh in mesh.MeshResource.SubMeshes)
                {
                    int matIndex = subMesh.MaterialIndex - minMatIndex;
                    if (matIndex < 0 || matIndex >= materials.Count) continue;

                    if (materials[matIndex].IsCull)
                        DrawCull(subMesh, materials[matIndex], transform);
                    else if (mesh.IsBillboard)
                        DrawBillbord(subMesh, materials[matIndex], transform);
                    else
                        DrawSubMesh(subMesh, materials[matIndex], transform.Transform.ToMatrix());
                }
            }
        }

        public void DrawCull(SubMeshResource subMesh, MaterialProperty material, TransformProperty transform)
        {
            BeginCull();
            DrawSubMesh(subMesh, material, transform.Transform.ToMatrix());
            EndCull();
        }

        public void DrawBillbord(SubMeshResource subMesh, MaterialProperty material, TransformProperty transform)
        {
            BeginBillbord();
            DrawSubMesh(subMesh, material, Engine.Graphics.Camera.GetBillboardMatrix(transform.Transform));
            material.ShaderResource.SetVector3("uColor", new Vector3(1f, 1f, 1f));
            material.ShaderResource.SetFloat("uAlpha", 1f);
            EndBillbord();
        }

        public void DrawSubMesh(SubMeshResource subMesh, MaterialProperty material, Matrix4x4 matrix)
        {
            material.Apply(matrix);
            subMesh.Draw();
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
