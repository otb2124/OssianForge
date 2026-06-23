using System.Numerics;
using OssianForge.Engine.Resources.Shaders;
using Silk.NET.OpenGL;

namespace OssianForge.Engine.Nodes.Props
{
    public class WireframeMaterialProperty : MaterialProperty
    {


        public Vector4 Color;
        public WireframeMaterialProperty(Vector4 color, string shaderId = "shader.wireframe")
            : base(shaderId)
        {
            Color = color;

            var gl = Engine.Graphics.Batch.OpenGL;
            BeginAction = () =>
            {
                gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
                gl.Disable(EnableCap.DepthTest);
            };
            EndAction = () =>
            {
                gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                gl.Enable(EnableCap.DepthTest);
            };
        }

        public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette)
        {
            ShaderResource.Use();
            ShaderResource.Apply(new ApplyContext
            {
                Model = model,
                View = view,
                Projection = projection,
                Palette = palette
            });
            // color is wireframe-specific, not part of ApplyContext
            ((WireframeShaderResource)ShaderResource).SetColor(Color);
        }
    }
}