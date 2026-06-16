using OssianForge.Engine.Resources.Shaders;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class SunEmissionProperty : EmissionProperty
    {
        /// <summary>
        /// World-space direction the light travels (towards the scene).
        /// e.g. Vector3(0.3f, -1f, 0.4f) normalized = afternoon sun.
        /// </summary>
        public Vector3 Direction;

        public SunEmissionProperty(Vector3 direction, Vector3 color, float intensity = 1f)
            : base(color, intensity)
        {
            Direction = Vector3.Normalize(direction);
        }

        public static SunEmissionProperty Noon(float intensity = 1f)
            => new SunEmissionProperty(new Vector3(0.2f, -1f, 0.3f), Vector3.One, intensity);

        public override LightData ToLightData(Node node) => new LightData
        {
            Type = LightType.Sun,
            Direction = Direction,
            Color = Color,
            Intensity = Intensity,
            // Position, Radius, cutoffs unused for sun
        };
    }
}