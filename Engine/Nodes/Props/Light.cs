using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class Light : NodeProperty
    {
        public Vector3 Color;
        public float Intensity;
        public float Radius;

        public Light(Vector3 color, float intensity = 1.0f, float radius = 10.0f)
        {
            Color = color;
            Intensity = intensity;
            Radius = radius;
        }

        public static Light White(float intensity = 1.0f, float radius = 10.0f)
            => new Light(Vector3.One, intensity, radius);
    }
}