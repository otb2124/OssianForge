using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OssianForge.Engine.Resources.Shaders;

public class SdfShaderResource : ShaderResource
{
    public SdfShaderResource(string id, params string[] shaderFileIds)
        : base(id, shaderFileIds) { }

    public override void Apply(ApplyContext ctx)
    {
        // Set the sampler to whatever unit the caller bound the texture to
        if (ctx.DiffuseTextureSlot.HasValue)
            SetInt("uTexture", (int)ctx.DiffuseTextureSlot.Value);

        SetMatrix4("uModel", ctx.Model);
        SetMatrix4("uView", ctx.View);
        SetMatrix4("uProjection", ctx.Projection);

        // uTextColor is set separately via SetVector4 before Apply() is called,
        // so we don't need to touch it here
    }
}
