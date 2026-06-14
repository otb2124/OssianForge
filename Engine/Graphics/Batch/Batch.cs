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

                DrawSubMesh(subMesh, materials[matIndex], transform.GetCameraModel(), transform.GetCameraView(), transform.GetCameraProjection(), palette);
            }
        }

        public void DrawSubMesh(SubMeshResource subMesh, MaterialProperty material, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] bonePalette)
        {
            material.BeginAction?.Invoke();
            material.Apply(model, view, projection, bonePalette);
            subMesh.Draw();
            material.PostApply();
            material.EndAction?.Invoke();
        }


        public unsafe uint CreateRenderTexture(float TextureWidth, float TextureHeight)
        {
            uint tex = OpenGL.GenTexture();
            OpenGL.BindTexture(TextureTarget.Texture2D, tex);
            OpenGL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          (uint)TextureWidth, (uint)TextureHeight, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, null);
            OpenGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            OpenGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            OpenGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            OpenGL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            OpenGL.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        public uint BeginOffscreenPass(uint _rtTexture, int TextureWidth, int TextureHeight)
        {
            uint fbo = OpenGL.GenFramebuffer();
            OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            OpenGL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                    FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, _rtTexture, 0);

            if (OpenGL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            {
                OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                OpenGL.DeleteFramebuffer(fbo);
                return 0;
            }

            OpenGL.Viewport(0, 0, (uint)TextureWidth, (uint)TextureHeight);
            OpenGL.ClearColor(0f, 0f, 0f, 0f);
            OpenGL.Clear(ClearBufferMask.ColorBufferBit);
            OpenGL.Disable(EnableCap.DepthTest);
            OpenGL.Enable(EnableCap.Blend);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            return fbo;
        }

        public void EndOffscreenPass(uint fbo)
        {
            OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            OpenGL.DeleteFramebuffer(fbo);

            OpenGL.Viewport(0, 0,
                (uint)Engine.Graphics.WindowSize.X,
                (uint)Engine.Graphics.WindowSize.Y);
            OpenGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            OpenGL.Enable(EnableCap.DepthTest);
        }

        public unsafe void UploadDynamicBuffer(uint vbo, float[] data)
        {
            OpenGL.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            fixed (float* ptr = data)
                OpenGL.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(data.Length * sizeof(float)),
                    ptr, BufferUsageARB.DynamicDraw);
            OpenGL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
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
