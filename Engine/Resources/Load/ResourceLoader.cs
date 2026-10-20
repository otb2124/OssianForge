using OssianForge.Content;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Resources
{
    public class ResourceLoader
    {
        public ResourcesConfig ResourcesConfig;


        public ResourceLoader()
        {
            ResourcesConfig = new ResourcesConfig("configfile.resources", "ConfigFiles/Core/resources.json");
        }

        public void InitializeCore()
        {
            // 1. Load base resource registry
            ResourcesConfig.Load();

            // 2. Load Tree and Scene configuration structures
            ResourcesConfig.BuildInstances<TreeConfig>();
            ResourcesConfig.LoadResources<TreeConfig>();

            ResourcesConfig.BuildInstances<SceneConfig>();
            ResourcesConfig.LoadResources<SceneConfig>();

            // 3. Find spawnable prefab scenes (root NetworkIdentityProperty with spawn flags) before networking starts
            Engine.Network.Replication.Prefabs.Discover(ResourcesConfig.GetInstances<SceneConfig>());
        }

        public void InitializeResourceConfigs()
        {
            var dependency = Engine.Resources.ResourceScheduler.NodeDependency;

            // Phase 1: Load pre-node resources (actions, pronouns, inputs) FIRST
            dependency.ExtractPreNodeResources();

            // Phase 2: Extract trees and scenes (nodes)
            dependency.ExtractTree("configfile.tree");
            dependency.ExtractScene(Engine.Resources.GetResource<TreeConfig>("configfile.tree").MainScene);

            // Spawnable prefabs are not referenced by any scene node, so pull in their resources explicitly
            foreach (var prefabSceneId in Engine.Network.Replication.Prefabs.SceneIds)
                dependency.ExtractScene(prefabSceneId);

            // Phase 3: Load post-node resources (modes) AFTER nodes exist
            dependency.ExtractPostNodeResources();

            // Build instances in sorted dependency order
            ResourcesConfig.BuildInstances(dependency.GetSortedResourceIds().ToArray());
        }



        public void OnLoad()
        {
            // Regular resources that depend on the initial load state
            ResourcesConfig.LoadResources(Engine.Resources.ResourceScheduler.NodeDependency.GetSortedResourceIds().ToArray());
        }

        public void PostLoad()
        {
            // Post-node resources (like ModesConfig) are built and loaded right after Nodes.OnLoad()
            ResourcesConfig.BuildInstances<ModesConfig>();
            ResourcesConfig.LoadResources<ModesConfig>();
        }
    }
}