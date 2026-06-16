using OssianForge.Engine.Resources.Shaders;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class SpotEmissionProperty : EmissionProperty
    {
        public Vector3 Direction;
        public float Radius;
        /// <summary>Full-bright cone angle in degrees.</summary>
        public float InnerAngle;
        /// <summary>Falloff edge angle in degrees.</summary>
        public float OuterAngle;

        public SpotEmissionProperty(Vector3 direction, Vector3 color,
            float intensity = 1f, float radius = 15f,
            float innerAngle = 12.5f, float outerAngle = 17.5f)
            : base(color, intensity)
        {
            Direction = Vector3.Normalize(direction);
            Radius = radius;
            InnerAngle = innerAngle;
            OuterAngle = outerAngle;
        }

        public override LightData ToLightData(Node node) => new LightData
        {
            Type = LightType.Spot,
            Position = node.GetProperty<TransformProperty>()?.Transform.Position ?? Vector3.Zero,
            Direction = Direction,
            Color = Color,
            Intensity = Intensity,
            Radius = Radius,
            InnerCutoff = MathF.Cos(float.DegreesToRadians(InnerAngle)),
            OuterCutoff = MathF.Cos(float.DegreesToRadians(OuterAngle)),
        };
    }
}