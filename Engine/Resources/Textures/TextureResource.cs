using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.TextureFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OssianForge.Engine.Resources.Textures
{
    public class TextureResource : Resource
    {

        public TextureFile Texture;
        public TextureFile NormalTexture;

        public string TextureId;
        public string TextureNormalId;


        public TextureResource(string id, string textureId, string normalTextureId = null)
        {
            Id = id;
            TextureId = textureId;
            TextureNormalId = normalTextureId;
        }

        public override void Load()
        {
            if (TextureId != null)
                Texture = Engine.Resources.GetResourceFile(TextureId) as TextureFile
                    ?? throw new Exception($"Texture not found: '{TextureId}'");

            if (TextureNormalId != null)
                NormalTexture = Engine.Resources.GetResourceFile(TextureNormalId) as TextureFile
                    ?? throw new Exception($"Normal texture not found: '{TextureNormalId}'");
        }


    }
}
