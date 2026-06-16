using OssianForge.Engine.Resources.Shaders;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public abstract class EmissionProperty : NodeProperty
    {
        public Vector3 Color;
        public float Intensity;

        protected EmissionProperty(Vector3 color, float intensity)
        {
            Color = color;
            Intensity = intensity;
        }

        /// <summary>
        /// Each subclass fills in the LightData fields it owns.
        /// Called by GetLights() — node is passed so position can be read.
        /// </summary>
        public abstract LightData ToLightData(Node node);
    }
}