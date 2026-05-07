using OssianForge.Engine.Nodes.Props;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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


        public void DrawMesh(MeshProperty mesh, List<MaterialProperty> materials, TransformProperty transform)
        {
            if (mesh != null)
            {
                int minMatIndex = mesh.MeshResource.SubMeshes.Count > 0 ? mesh.MeshResource.SubMeshes.Min(s => s.MaterialIndex) : 0;

                foreach (var subMesh in mesh.MeshResource.SubMeshes)
                {
                    int matIndex = subMesh.MaterialIndex - minMatIndex;
                    if (matIndex < 0 || matIndex >= materials.Count) continue;

                    if(materials[matIndex].IsCull)
                        BeginCull();

                    materials[matIndex].Apply(transform.Transform);
                    subMesh.Draw();

                    if (materials[matIndex].IsCull)
                        EndCull();
                }
            }
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
