using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{

    //add Update and Render
    public class SkyboxProperty : NodeProperty
    {
        public Vector3 TopColor;
        public Vector3 BottomColor;

        public SkyboxProperty(Vector3 topColor, Vector3 bottomColor)
        {
            TopColor = topColor;
            BottomColor = bottomColor;
        }
    }
}