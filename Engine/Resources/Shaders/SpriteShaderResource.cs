using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Shaders
{
    // Example of a shader with its own extra data beyond the standard context
    public class SpriteShaderResource : ShaderResource
    {
        public SpriteShaderResource(string id, params string[] shaderFileIds)
            : base(id, shaderFileIds) { }

        public override void Apply(ApplyContext ctx)
        {
            if (ctx.DiffuseTextureSlot.HasValue)
                SetInt("uTexture", (int)ctx.DiffuseTextureSlot.Value);

            SetMatrix4("uModel", ctx.Model);
            SetMatrix4("uView", ctx.View);
            SetMatrix4("uProjection", ctx.Projection);
            SetVector3("uColor", new Vector3(1f, 1f, 1f));
            SetFloat("uAlpha", 1f);
        }
    }
}
