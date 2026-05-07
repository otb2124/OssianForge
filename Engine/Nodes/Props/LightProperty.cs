using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class LightProperty : NodeProperty
    {
        public Vector3 Color;
        public float Intensity;
        public float Radius;

        public LightProperty(Vector3 color, float intensity = 1.0f, float radius = 10.0f)
        {
            Color = color;
            Intensity = intensity;
            Radius = radius;
        }

        public static LightProperty White(float intensity = 1.0f, float radius = 10.0f)
            => new LightProperty(Vector3.One, intensity, radius);
    }
}