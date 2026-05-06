using Silk.NET.OpenGL;
using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Engine.Resources.ShaderFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class Mesh : NodeProperty, IDisposable
    {
        protected uint _vao, _vbo;
        protected uint _vertexCount;
        public int MaterialIndex;

        public Mesh() { } // for subclasses

        public Mesh(float[] vertices, int materialIndex = 0, bool hasUV = false, bool hasNormals = false)
        {
            MaterialIndex = materialIndex;
            Init(vertices, hasUV, hasNormals);
        }

        protected void Init(float[] vertices, bool hasUV, bool hasNormals)
        {
            int stride = 3;
            if (hasNormals) stride += 3;
            if (hasUV) stride += 2;
            _vertexCount = (uint)(vertices.Length / stride);

            var gl = Engine.Graphics.OpenGL;
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* ptr = vertices)
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(vertices.Length * sizeof(float)),
                        ptr, BufferUsageARB.StaticDraw);
            }

            int offset = 0;

            gl.EnableVertexAttribArray(0);
            unsafe { gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
            offset += 3;

            if (hasNormals)
            {
                gl.EnableVertexAttribArray(1);
                unsafe { gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
                offset += 3;
            }

            if (hasUV)
            {
                uint uvLoc = hasNormals ? 2u : 1u;
                gl.EnableVertexAttribArray(uvLoc);
                unsafe { gl.VertexAttribPointer(uvLoc, 2, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
            }

            gl.BindVertexArray(0);
        }

        public virtual void Draw()
        {
            Engine.Graphics.OpenGL.BindVertexArray(_vao);
            Engine.Graphics.OpenGL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
        }

        public virtual void Dispose()
        {
            Engine.Graphics.OpenGL.DeleteVertexArray(_vao);
            Engine.Graphics.OpenGL.DeleteBuffer(_vbo);
        }
    }


    
}
