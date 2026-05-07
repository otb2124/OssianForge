using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Graphics.RenderTarget
{
    public class RenderTarget : IDisposable
    {
        private uint _fbo;
        private uint _colorTexture;
        private uint _depthRbo;

        public uint ColorTexture => _colorTexture;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public RenderTarget(int width, int height)
        {
            Width = width;
            Height = height;
            Build(width, height);
        }

        private unsafe void Build(int w, int h)
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            // --- Framebuffer ---
            _fbo = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            // HDR color texture (RGBA16f so tonemapping works properly)
            _colorTexture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f,
                          (uint)w, (uint)h, 0,
                          PixelFormat.Rgba, PixelType.Float, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                    FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, _colorTexture, 0);

            // Depth+Stencil renderbuffer — we never sample depth in post-processing
            _depthRbo = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                                   InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                                       FramebufferAttachment.DepthStencilAttachment,
                                       RenderbufferTarget.Renderbuffer, _depthRbo);

            var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new Exception($"RenderTarget FBO incomplete: {status}");

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Bind()
        {
            Engine.Graphics.Batch.OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        }

        public static void BindDefault()
        {
            Engine.Graphics.Batch.OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(int w, int h)
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.DeleteFramebuffer(_fbo);
            gl.DeleteTexture(_colorTexture);
            gl.DeleteRenderbuffer(_depthRbo);
            Width = w; Height = h;
            Build(w, h);
        }

        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.DeleteFramebuffer(_fbo);
            gl.DeleteTexture(_colorTexture);
            gl.DeleteRenderbuffer(_depthRbo);
        }
    }
}
