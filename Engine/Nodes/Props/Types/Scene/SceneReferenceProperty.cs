using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Nodes.Props.Types.Scene
{
    public class SceneReferenceProperty : NodeProperty
    {
        public string SceneId;

        public SceneReferenceProperty(string sceneId)
        {
            SceneId = sceneId;
        }
    }
}