using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class CubemapMaterialProperty : MaterialProperty
    {
        public CubemapTextureResource CubemapResource;

        public CubemapMaterialProperty(string cubemapId, string shaderId)
            : base(shaderId)
        {
            CubemapResource = Engine.Resources.GetResource(cubemapId) as CubemapTextureResource
                ?? throw new Exception($"CubemapTextureResource not found: '{cubemapId}'");
        }

        public override void Apply(Matrix4x4 transform)
        {
            Engine.Graphics.Batch.OpenGL.DepthFunc(DepthFunction.Lequal);
            Engine.Graphics.Batch.OpenGL.DepthMask(false);

            ShaderResource.Use();
            CubemapResource.Bind(0);

            var (_, viewNoTranslation) = GetViewMatrices();

            ShaderResource.Apply(new ApplyContext
            {
                ViewNoTranslation = viewNoTranslation,
                Projection = Engine.Graphics.GetCurrentCamera().GetProjection(),
                SkyboxTextureSlot = 0,
            });
        }

        public override void PostApply()
        {
            Engine.Graphics.Batch.OpenGL.DepthMask(true);
            Engine.Graphics.Batch.OpenGL.DepthFunc(DepthFunction.Less);
        }
    }
}
