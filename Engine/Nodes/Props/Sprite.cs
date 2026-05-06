using OssianForge.Engine.Resources.TextureFiles;
using System.Numerics;
using Silk.NET.OpenGL;

namespace OssianForge.Engine.Nodes.Props
{
    public class Sprite : NodeProperty, IDisposable
    {
        public TextureFile Texture;
        public Vector2 Size;
        public Vector4 Color;
        public Model Model;

        private Shader _shader;
        private uint _vao, _vbo;

        private static readonly float[] QuadVertices =
        {
            -0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
        };

        // Texture-based billboard
        public Sprite(string textureId, Vector2 size, Vector4 color = default)
        {
            Texture = Engine.Resources.GetResourceFile(textureId) as TextureFile
                ?? throw new Exception($"Texture not found: '{textureId}'");
            Size = size;
            Color = color == default ? Vector4.One : color;
            _shader = new Shader("shaderfile.sprite.vert", "shaderfile.sprite.frag");
            SetupQuad();
        }

        public Sprite(Model model)
        {
            Model = model;
        }


        private void SetupQuad()
        {
            var gl = Engine.Graphics.OpenGL;
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* ptr = QuadVertices)
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(QuadVertices.Length * sizeof(float)),
                        ptr, BufferUsageARB.StaticDraw);
            }

            gl.EnableVertexAttribArray(0);
            unsafe { gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*)0); }

            gl.EnableVertexAttribArray(1);
            unsafe { gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), (void*)(3 * sizeof(float))); }

            gl.BindVertexArray(0);
        }

        public void Draw(Vector3 worldPosition, Vector3 worldScale)
        {
            var camera = Engine.Graphics.Camera;
            var view = camera.GetView();
            var proj = camera.GetProjection();

            var right = new Vector3(view.M11, view.M21, view.M31);
            var up = new Vector3(view.M12, view.M22, view.M32);
            var forward = new Vector3(view.M13, view.M23, view.M33);

            var billboard = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1
            );

            var gl = Engine.Graphics.OpenGL;

            // Sprites are transparent — don't write to depth, read only
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            gl.DepthMask(false);   // don't write to depth buffer
            gl.Enable(EnableCap.DepthTest);

            if (Model != null)
            {
                Matrix4x4 model =
                    Matrix4x4.CreateScale(worldScale) *
                    billboard *
                    Matrix4x4.CreateTranslation(worldPosition);

                Model.Draw(model, view, proj);
            }
            else if (_vao != 0)
            {
                Matrix4x4 model =
                    Matrix4x4.CreateScale(Size.X * worldScale.X, Size.Y * worldScale.Y, 1.0f) *
                    billboard *
                    Matrix4x4.CreateTranslation(worldPosition);

                _shader.Use();
                _shader.SetMatrix4("uModel", model);
                _shader.SetMatrix4("uView", view);
                _shader.SetMatrix4("uProjection", proj);
                _shader.SetVector3("uColor", new Vector3(Color.X, Color.Y, Color.Z));
                _shader.SetFloat("uAlpha", Color.W);

                Texture.Bind(0);
                _shader.SetInt("uTexture", 0);

                gl.BindVertexArray(_vao);
                gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
                gl.BindVertexArray(0);
            }

            // Restore depth writing for everything else
            gl.DepthMask(true);
            gl.Disable(EnableCap.Blend);
        }

        public void Dispose()
        {
            var gl = Engine.Graphics.OpenGL;
            if (_vao != 0) gl.DeleteVertexArray(_vao);
            if (_vbo != 0) gl.DeleteBuffer(_vbo);
            Model?.Dispose();
        }
    }
}