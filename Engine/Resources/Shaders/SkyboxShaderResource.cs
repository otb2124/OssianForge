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
            SetVector3("uTopColor", new Vector3(0.4f, 0.6f, 1.0f));
            SetVector3("uBottomColor", new Vector3(0.8f, 0.85f, 1.0f));
            SetMatrix4("uView", ctx.ViewNoTranslation);
            SetMatrix4("uProjection", ctx.Projection);
            SetMatrix4("uModel", Matrix4x4.Identity);
        }
    }
}
