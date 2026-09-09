using OssianForge.Engine.Resources.Shaders;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class ColorMaterialProperty : MaterialProperty
    {
        public Vector4 Color;

        public ColorMaterialProperty(Vector4 color, string shaderId, params RenderAction[] actions) : base(shaderId, actions)
        {
            Color = color;
        }

        public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette)
        {
            ShaderResource.Use();

            ShaderResource.Apply(new ApplyContext
            {
                Model = model,
                View = view,
                Projection = projection,
                ViewNoTranslation = Engine.Graphics.GetCurrentCamera().GetViewNoTranslation(),
                BaseColor = Color,
                DiffuseTextureSlot = null,
                NormalTextureSlot = null,
                HasNormalTexture = false,
                Lights = Engine.Nodes.NodeManager.GetLights(),
                Palette = palette
            });
        }
    }
}