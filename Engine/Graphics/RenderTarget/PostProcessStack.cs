using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Graphics.RenderTarget
{
    public class PostProcessStack : IDisposable
    {
        // Ping-pong between two targets so passes can chain
        private RenderTarget _a;
        private RenderTarget _b;

        // The target the scene renders INTO (pass this to Graphics)
        public RenderTarget SceneTarget => _a;

        private uint _quadVao;
        private uint _quadVbo;

        public List<PostProcessPass> Passes = new();

        public PostProcessStack(int width, int height)
        {
            _a = new RenderTarget(width, height);
            _b = new RenderTarget(width, height);
            CreateQuad();
        }

        private unsafe void CreateQuad()
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            // NDC fullscreen quad: XY position + UV
            float[] verts = {
               -1f,  1f,  0f, 1f,
               -1f, -1f,  0f, 0f,
                1f, -1f,  1f, 0f,
               -1f,  1f,  0f, 1f,
                1f, -1f,  1f, 0f,
                1f,  1f,  1f, 1f,
            };

            _quadVao = gl.GenVertexArray();
            _quadVbo = gl.GenBuffer();

            gl.BindVertexArray(_quadVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);

            fixed (float* p = verts)
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                              (nuint)(verts.Length * sizeof(float)),
                              p, BufferUsageARB.StaticDraw);

            uint stride = 4 * sizeof(float);
            gl.EnableVertexAttribArray(0);  // position
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);  // uv
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

            gl.BindVertexArray(0);
        }

        /// <summary>
        /// Call this INSTEAD of your manual Clear+Render. 
        /// Binds the scene FBO so your normal OnRender draws into it.
        /// </summary>
        public void BeginScene()
        {
            _a.Bind();
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        /// <summary>
        /// Call at the END of OnRender. Runs all enabled passes and blits to screen.
        /// </summary>
        public void EndScene()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.Disable(EnableCap.DepthTest); // post is purely 2D

            var enabledPasses = Passes.Where(p => p.Enabled).ToList();

            // source always starts as the scene render
            RenderTarget src = _a;
            RenderTarget dst = _b;

            for (int i = 0; i < enabledPasses.Count; i++)
            {
                bool isLastPass = i == enabledPasses.Count - 1;

                if (isLastPass)
                    RenderTarget.BindDefault();  // final pass → screen
                else
                    dst.Bind();                  // intermediate pass → ping-pong buffer

                gl.Clear(ClearBufferMask.ColorBufferBit);

                var pass = enabledPasses[i];
                pass.ApplyUniforms(src.ColorTexture);

                gl.BindVertexArray(_quadVao);
                gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
                gl.BindVertexArray(0);

                // swap for next pass
                (src, dst) = (dst, src);
            }

            // No passes? Just blit _a to screen with a passthrough
            if (enabledPasses.Count == 0)
            {
                RenderTarget.BindDefault();
                gl.Clear(ClearBufferMask.ColorBufferBit);
                // You'd need a passthrough shader here — see note below
            }

            gl.Enable(EnableCap.DepthTest);
        }

        public void Resize(int w, int h)
        {
            _a.Resize(w, h);
            _b.Resize(w, h);
        }

        public void Dispose()
        {
            _a.Dispose();
            _b.Dispose();
            Engine.Graphics.Batch.OpenGL.DeleteVertexArray(_quadVao);
            Engine.Graphics.Batch.OpenGL.DeleteBuffer(_quadVbo);
            foreach (var p in Passes) p.Dispose();
        }
    }
}
