using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Shaders
{
    public class SkyboxShaderResource : ShaderResource
    {
        public SkyboxShaderResource(string id, params string[] shaderFileIds)
            : base(id, shaderFileIds) { }

        public override void Apply(ApplyContext ctx)
        {
            if (ctx.SkyboxTextureSlot.HasValue)
                SetInt("uSkybox", (int)ctx.SkyboxTextureSlot.Value);

            SetMatrix4("uView", ctx.ViewNoTranslation);
            SetMatrix4("uProjection", ctx.Projection);
            SetMatrix4("uModel", Matrix4x4.Identity);
        }
    }
}
