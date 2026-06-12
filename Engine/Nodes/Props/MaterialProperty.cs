using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public class MaterialProperty : NodeProperty
    {
        public ShaderResource ShaderResource;

        public MaterialProperty(string shaderId)
        {
            ShaderResource = Engine.Resources.GetResource(shaderId) as ShaderResource
                ?? throw new Exception($"ShaderResource not found: '{shaderId}'");
        }

        public virtual void Apply(Matrix4x4 transform, Matrix4x4[] palette) { }

        public virtual void PostApply() { }

        

        protected List<LightData> GetLights()
            => Engine.Nodes.NodeManager
                .GetNodesOfType(typeof(Node))
                .Select(n => new { Node = n, Emission = n.GetProperty<EmissionProperty>() })
                .Where(x => x.Emission != null)
                .Select(x => new LightData
                {
                    Position = x.Node.GetProperty<TransformProperty>().Transform.Position,
                    Color = x.Emission.Color,
                    Intensity = x.Emission.Intensity,
                    Radius = x.Emission.Radius,
                })
                .Take(16)
                .ToList();
    }
}
