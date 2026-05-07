using OssianForge.Engine.Resources.TextureFiles;
using System.Numerics;
using Silk.NET.OpenGL;
using OssianForge.Engine.Resources.Shaders;


namespace OssianForge.Engine.Nodes.Props
{


    //TODO: replace with just adding MaterialProperty, QuadMesh and ShaderProperty to Node3D
    //Move Billboard feature to MeshProperty
    public class SpriteProperty : NodeProperty, IDisposable
    {
        public Vector2 Size;
        public Vector4 Color;

        public MaterialProperty _materialProperty;
        public MeshProperty _meshProperty;

        // Texture-based billboard
        public SpriteProperty(string textureId, Vector2 size, Vector4 color = default)
        {
            Size = size;
            Color = color == default ? Vector4.One : color;

            _materialProperty = new MaterialProperty(textureId, "shader.sprite");
            _meshProperty = new MeshProperty("mesh.quad");
        }


        public void Draw(Vector3 worldPosition, Vector3 worldScale)
        {
            var camera = Engine.Graphics.Camera;
            var view = camera.GetView();

            Matrix4x4.Invert(view, out var invView);
            var right = new Vector3(invView.M11, invView.M12, invView.M13);
            var up = new Vector3(invView.M21, invView.M22, invView.M23);
            var forward = new Vector3(invView.M31, invView.M32, invView.M33);

            var billboard = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1
            );

            Matrix4x4 model =
                Matrix4x4.CreateScale(Size.X * worldScale.X, Size.Y * worldScale.Y, 1.0f) *
                billboard *
                Matrix4x4.CreateTranslation(worldPosition);

            var gl = Engine.Graphics.Batch.OpenGL;
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            gl.DepthMask(false);   // don't write to depth buffer
            gl.Enable(EnableCap.DepthTest);

            Engine.Graphics.Batch.DrawMesh(_meshProperty, new List<MaterialProperty> { _materialProperty }, model);

            _materialProperty.ShaderResource.SetVector3("uColor", new Vector3(Color.X, Color.Y, Color.Z));
            _materialProperty.ShaderResource.SetFloat("uAlpha", Color.W);

            // Restore
            gl.DepthMask(true);
            gl.DepthFunc(DepthFunction.Less);
            gl.Disable(EnableCap.Blend);
        }

        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            //Model?.Dispose();
        }
    }
}