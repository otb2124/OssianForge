using OssianForge.Engine.Resources.Shaders;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class PointEmissionProperty : EmissionProperty
    {
        public float Radius;

        public PointEmissionProperty(Vector3 color, float intensity = 1f, float radius = 10f)
            : base(color, intensity)
        {
            Radius = radius;
        }

        public static PointEmissionProperty White(float intensity = 1f, float radius = 10f)
            => new PointEmissionProperty(Vector3.One, intensity, radius);

        public override LightData ToLightData(Node node) => new LightData
        {
            Type = LightType.Point,
            Position = node.GetProperty<TransformProperty>()?.Transform.Position ?? Vector3.Zero,
            Color = Color,
            Intensity = Intensity,
            Radius = Radius,
        };
    }
}