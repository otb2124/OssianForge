using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Shaders
{
    public class BasicShaderResource : ShaderResource
    {
        public BasicShaderResource(string id, params string[] shaderFileIds)
            : base(id, shaderFileIds) { }

        public override void Apply(ApplyContext ctx)
        {
            if (ctx.DiffuseTextureSlot.HasValue)
                SetInt("uTexture", (int)ctx.DiffuseTextureSlot.Value);

            SetInt("uHasNormalTexture", ctx.HasNormalTexture ? 1 : 0);

            if (ctx.NormalTextureSlot.HasValue)
                SetInt("uNormalTexture", (int)ctx.NormalTextureSlot.Value);

            SetMatrix4("uModel", ctx.Model);
            SetMatrix4("uView", ctx.View);
            SetMatrix4("uProjection", ctx.Projection);

            var lights = ctx.Lights ?? new List<LightData>();
            SetInt("uLightCount", lights.Count);

            for (int i = 0; i < lights.Count; i++)
            {
                SetVector3Indexed("uLights", i, "position", lights[i].Position);
                SetVector3Indexed("uLights", i, "color", lights[i].Color);
                SetFloatIndexed("uLights", i, "intensity", lights[i].Intensity);
                SetFloatIndexed("uLights", i, "radius", lights[i].Radius);
            }

            if(ctx.Palette != null && ctx.Palette.Length > 0)
            {
                SetInt("uSkinned", 1);
                for (int i = 0; i < ctx.Palette.Length; i++)
                {
                    SetMatrix4($"uBones[{i}]", ctx.Palette[i]);
                }
            }
            else
            {
                SetInt("uSkinned", 0);
            }
            
             
            
        }
    }
}
