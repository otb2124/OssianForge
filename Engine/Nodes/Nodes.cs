using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Nodes
{
    public class Nodes
    {

        public NodeManager NodeManager;

        /// <summary>Root of the scene currently attached under the tree node. Null before OnLoad.</summary>
        public Node CurrentScene { get; private set; }
        public string CurrentSceneId { get; private set; }

        private Node _sceneParent;

        public Nodes() { NodeManager = new NodeManager(); }


        public void Initialize()
        {
            NodeManager.Initialize();
        }

        public void OnLoad()
        {
            var treeConfig = Engine.Resources.GetResource<TreeConfig>("configfile.tree");
            var tree = treeConfig.GetTreeNode();
            _sceneParent = tree;

            AttachScene(treeConfig.MainScene, start: false);

            //script packs test
            // ... your commented-out block stays here unchanged ...

            NodeManager.RegisterTree(tree);

            NodeManager.OnStart();
        }

        /// <summary>
        /// Replaces the current scene. Call on the update thread (from the console: NodeManager.Enqueue).
        /// </summary>
        public void ChangeScene(string sceneId)
        {
            if (CurrentScene == null)
                throw new InvalidOperationException("No scene is loaded yet.");
            if (Engine.Network.IsOnline)
                throw new InvalidOperationException("Scene changes are not replicated; run 'net stop' first.");

            var sceneConfig = Engine.Resources.GetResource<SceneConfig>(sceneId)
                ?? throw new ArgumentException($"'{sceneId}' is not a scene.");

            // 1. Load what the new scene needs. Anything already instantiated is left alone so
            //    references held by surviving nodes (tree root, sounds, ...) stay valid.
            var resources = Engine.Resources.ResourceLoader.ResourcesConfig;
            var pending = Engine.Resources.ResourceScheduler.NodeDependency
                .AddScene(sceneId)
                .Where(id => resources.GetInstanceById(id) == null)
                .ToArray();
            if (pending.Length > 0)
            {
                resources.BuildInstances(pending);
                resources.LoadResources(pending);
            }

            // 2. Tear down BEFORE building: node ids are the id-cache keys, so an old and a new scene
            //    sharing ids would unregister each other.
            string previousId = CurrentSceneId;
            NodeManager.DestroySubtree(CurrentScene);
            CurrentScene = null;

            // 3. Build + start the new one; fall back to the old one if the build blows up.
            try
            {
                AttachScene(sceneId, start: true);
            }
            catch
            {
                AttachScene(previousId, start: true);
                throw;
            }

            Engine.Graphics.CurrentCameraNode = null; // re-picked by GetCurrentCamera()
        }

        private void AttachScene(string sceneId, bool start)
        {
            var scene = Engine.Resources.GetResource<SceneConfig>(sceneId).GetScene();
            _sceneParent.AddChild(scene);
            if (start) NodeManager.StartSubtree(scene);

            CurrentScene = scene;
            CurrentSceneId = sceneId;
        }

        public void OnUpdate(double delta)
        {
            NodeManager.OnUpdate(delta);
        }

        public void OnRender(double delta)
        {
            NodeManager.OnRender(delta);
        }
    }
}