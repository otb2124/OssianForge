using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace OssianForge.Engine.Resources.Textures
{
    public class ProbeTextureResource : Resource
    {
        public uint Handle;
        public uint FramebufferHandle;
        public uint DepthRenderbufferHandle;
        public int FaceSize;

        public Vector3 Position;

        public ProbeTextureResource(string id, int faceSize = 256)
        {
            Id = id;
            FaceSize = faceSize;
        }

        public override void Load()
        {
            base.Load();
            var gl = Engine.Graphics.Batch.OpenGL;

            Handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.TextureCubeMap, Handle);

            var targets = new[]
            {
                TextureTarget.TextureCubeMapPositiveX,
                TextureTarget.TextureCubeMapNegativeX,
                TextureTarget.TextureCubeMapPositiveY,
                TextureTarget.TextureCubeMapNegativeY,
                TextureTarget.TextureCubeMapPositiveZ,
                TextureTarget.TextureCubeMapNegativeZ,
            };

            unsafe
            {
                foreach (var target in targets)
                {
                    gl.TexImage2D(target, 0, InternalFormat.Rgba8,
                        (uint)FaceSize, (uint)FaceSize, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, null);
                }
            }

            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            gl.BindTexture(TextureTarget.TextureCubeMap, 0);

            DepthRenderbufferHandle = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthRenderbufferHandle);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24,
                (uint)FaceSize, (uint)FaceSize);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            FramebufferHandle = gl.GenFramebuffer();
        }

        public unsafe void Render(double delta, float nearPlane = 0.05f, float farPlane = 500f)
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            var targets = new[]
            {
                TextureTarget.TextureCubeMapPositiveX,
                TextureTarget.TextureCubeMapNegativeX,
                TextureTarget.TextureCubeMapPositiveY,
                TextureTarget.TextureCubeMapNegativeY,
                TextureTarget.TextureCubeMapPositiveZ,
                TextureTarget.TextureCubeMapNegativeZ,
            };

            var directions = new Vector3[]
            {
                new Vector3( 1,  0,  0), // +X
                new Vector3(-1,  0,  0), // -X
                new Vector3( 0,  1,  0), // +Y
                new Vector3( 0, -1,  0), // -Y
                new Vector3( 0,  0,  1), // +Z
                new Vector3( 0,  0, -1), // -Z
            };

            var camera = Engine.Graphics.GetCurrentCamera();
            if (camera == null)
                throw new Exception("ProbeTextureResource.Render: no active camera found in scene.");

            Vector3 savedPosition = camera.Position;
            float savedFov = camera.Fov;
            float savedAspect = camera.AspectRatio;
            float savedNear = camera.NearPlane;
            float savedFar = camera.FarPlane;
            Matrix4x4 savedView = camera.GetView();

            camera.Position = Position;
            camera.Fov = 90f;
            camera.AspectRatio = 1.0f;
            camera.NearPlane = nearPlane;
            camera.FarPlane = farPlane;

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, DepthRenderbufferHandle);
            gl.Viewport(0, 0, (uint)FaceSize, (uint)FaceSize);

            try
            {
                for (int i = 0; i < 6; i++)
                {
                    gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                        FramebufferAttachment.ColorAttachment0,
                        targets[i], Handle, 0);

                    if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                        throw new Exception($"Probe framebuffer incomplete on face {i}");

                    gl.Enable(EnableCap.DepthTest);
                    gl.ClearColor(0f, 0f, 0f, 1f);
                    gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    camera.SetLookDirection(directions[i]);

                    Engine.Nodes.NodeManager.OnRender(delta);
                }
            }
            finally
            {
                camera.Position = savedPosition;
                camera.Fov = savedFov;
                camera.AspectRatio = savedAspect;
                camera.NearPlane = savedNear;
                camera.FarPlane = savedFar;
                camera.SetViewMatrix(savedView);
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            gl.Viewport(0, 0,
                (uint)Engine.Graphics.WindowSize.X,
                (uint)Engine.Graphics.WindowSize.Y);

            gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
            gl.GenerateMipmap(TextureTarget.TextureCubeMap);
            gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        }

        public void Bind(uint slot = 0)
        {
            Engine.Graphics.Batch.OpenGL.ActiveTexture(TextureUnit.Texture0 + (int)slot);
            Engine.Graphics.Batch.OpenGL.BindTexture(TextureTarget.TextureCubeMap, Handle);
        }

        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.DeleteTexture(Handle);
            gl.DeleteFramebuffer(FramebufferHandle);
            gl.DeleteRenderbuffer(DepthRenderbufferHandle);
        }
    }
}