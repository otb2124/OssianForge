using System.Numerics;

namespace OssianForge.Engine.Resources.Shaders
{
    public class WireframeShaderResource : ShaderResource
    {
        public WireframeShaderResource(string id, params string[] shaderFileIds)
            : base(id, shaderFileIds) { }

        public override void Apply(ApplyContext ctx)
        {
            SetMatrix4("uModel", ctx.Model);
            SetMatrix4("uView", ctx.View);
            SetMatrix4("uProjection", ctx.Projection);

            if (ctx.Palette != null && ctx.Palette.Length > 0)
            {
                SetInt("uSkinned", 1);
                for (int i = 0; i < ctx.Palette.Length && i < 100; i++)
                    SetMatrix4($"uBones[{i}]", ctx.Palette[i]);
            }
            else
            {
                SetInt("uSkinned", 0);
            }
        }

        public void SetColor(Vector4 color) => SetVector4("uColor", color);
    }
}