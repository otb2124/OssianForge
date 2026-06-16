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
            TextureResource = Engine.Resources.GetResource<TextureResource>(textureId)
                ?? throw new Exception($"TextureResource not found: '{textureId}'");
        }

        public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette)
        {
            ShaderResource.Use();

            uint? diffuseSlot = null;
            uint? normalSlot = null;

            if (TextureResource.TextureFiles[0] != null)
            {
                TextureResource.TextureFiles[0].Bind(0);
                diffuseSlot = 0;
            }
            if (TextureResource.TextureFiles.Count > 1 && TextureResource.TextureFiles[1] != null)
            {
                TextureResource.TextureFiles[1].Bind(1);
                normalSlot = 1;
            }

            ShaderResource.Apply(new ApplyContext
            {
                Model = model,
                View = view,
                Projection = projection,
                ViewNoTranslation = Engine.Graphics.GetCurrentCamera().GetViewNoTranslation(),
                DiffuseTextureSlot = diffuseSlot,
                NormalTextureSlot = normalSlot,
                HasNormalTexture = normalSlot.HasValue,
                Lights = Engine.Nodes.NodeManager.GetLights(),
                Palette = palette
            });
        }
    }
}
