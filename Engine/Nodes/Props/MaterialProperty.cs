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

        public virtual void Apply(Matrix4x4 transform) { }

        public virtual void PostApply() { }

        protected (Matrix4x4 view, Matrix4x4 viewNoTranslation) GetViewMatrices()
        {
            var view = Engine.Graphics.GetCurrentCamera().GetView();
            var viewNoTranslation = new Matrix4x4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1);
            return (view, viewNoTranslation);
        }

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
