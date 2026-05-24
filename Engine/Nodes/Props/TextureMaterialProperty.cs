using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class TextureMaterialProperty : MaterialProperty
    {
        public TextureResource TextureResource;

        public TextureMaterialProperty(string textureId, string shaderId)
            : base(shaderId)
        {
            TextureResource = Engine.Resources.GetResource(textureId) as TextureResource
                ?? throw new Exception($"TextureResource not found: '{textureId}'");
        }

        public override void Apply(Matrix4x4 transform)
        {
            ShaderResource.Use();

            uint? diffuseSlot = null;
            uint? normalSlot = null;

            if (TextureResource.Texture != null)
            {
                TextureResource.Texture.Bind(0);
                diffuseSlot = 0;
            }
            if (TextureResource.NormalTexture != null)
            {
                TextureResource.NormalTexture.Bind(1);
                normalSlot = 1;
            }

            var (view, viewNoTranslation) = GetViewMatrices();

            ShaderResource.Apply(new ApplyContext
            {
                Model = transform,
                View = view,
                Projection = Engine.Graphics.GetCurrentCamera().GetProjection(),
                ViewNoTranslation = viewNoTranslation,
                DiffuseTextureSlot = diffuseSlot,
                NormalTextureSlot = normalSlot,
                HasNormalTexture = normalSlot.HasValue,
                Lights = GetLights(),
            });
        }
    }
}
