using OssianForge.Content;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Resources
{
    public class ResourceLoader
    {
        public ResourcesConfig ResourcesConfig;

        public static IContentSource Source { get; private set; } = new DiskContentSource(Resource.CONTENT_FOLDER_PATH);

        /// <summary>True when Source returns cooked blobs; loaders then use the OssianContent codecs instead of importing.</summary>
        public static bool IsCooked => Source.IsCooked;


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
        }

        public void InitializeResourceConfigs()
        {
            var dependency = Engine.Resources.ResourceScheduler.NodeDependency;

            // Phase 1: Load pre-node resources (actions, pronouns, inputs) FIRST
            dependency.ExtractPreNodeResources();

            // Phase 2: Extract trees and scenes (nodes)
            dependency.ExtractTree("configfile.tree");
            dependency.ExtractScene(Engine.Resources.GetResource<TreeConfig>("configfile.tree").MainScene);

            // Phase 3: Load post-node resources (modes) AFTER nodes exist
            dependency.ExtractPostNodeResources();

            // Build instances in sorted dependency order
            ResourcesConfig.BuildInstances(dependency.GetSortedResourceIds().ToArray());
        }

        public static void InitializeResources(string? packPath = null)
        {
            if (Environment.GetEnvironmentVariable("OSSIAN_CONTENT") == "loose") return;

            packPath ??= System.IO.Path.Combine(AppContext.BaseDirectory, "Content.pak");
            if (!System.IO.File.Exists(packPath)) return;

            Source = new Content.Pack.PackContentSource(packPath);
            Console.WriteLine($"[CONTENT] Using cooked pack: {packPath}");
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
