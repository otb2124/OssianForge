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

        public override void OnStart(Node node)
        {
            base.OnStart(node);

            var sceneConfig = Engine.Resources.GetResourceFile<SceneConfig>(SceneId)
                ?? throw new Exception($"[SCENE REFERENCE] SceneConfig '{SceneId}' not found.");

            var referencedRoot = sceneConfig.GetScene();

            node.AddChild(referencedRoot);

        }
    }
}