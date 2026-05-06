using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class Skybox : NodeProperty
    {
        public Vector3 TopColor;
        public Vector3 BottomColor;

        public Skybox(Vector3 topColor, Vector3 bottomColor)
        {
            TopColor = topColor;
            BottomColor = bottomColor;
        }
    }
}