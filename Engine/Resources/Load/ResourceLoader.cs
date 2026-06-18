using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Resources.ShaderFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    public class ResourceLoader
    {

        public ResourceFilesConfig ResourceFilesConfig;
        public ResourcesConfig ResourcesConfig;

        public ResourceLoader()
        {
            ResourceFilesConfig = new ResourceFilesConfig("configfile.resourceFiles", "ConfigFiles/Core/resourceFiles.json");
            ResourcesConfig = new ResourcesConfig("configfile.resources", "ConfigFiles/Core/resources.json");
        }

        public void InitializeCore()
        {
            ResourceFilesConfig.Load();
            ResourcesConfig.Load();

            ResourceFilesConfig.BuildInstances<SceneConfig>();
            ResourceFilesConfig.LoadResourceFiles<SceneConfig>(); 

            ResourceFilesConfig.BuildInstances<TreeConfig>();
            ResourceFilesConfig.LoadResourceFiles<TreeConfig>();
        }

        public void InitializeResources()
        {
            ResourceFilesConfig.BuildInstances(Engine.Resources.ResourceScheduler.NodeDependency.ResourceFileIds.ToArray<string>());
            ResourcesConfig.BuildInstances(Engine.Resources.ResourceScheduler.NodeDependency.GetSortedResourceIds().ToArray());
        }

        public void OnLoad()
        {
            ResourceFilesConfig.LoadResourceFiles(Engine.Resources.ResourceScheduler.NodeDependency.ResourceFileIds.ToArray<string>());
            ResourcesConfig.LoadResources(Engine.Resources.ResourceScheduler.NodeDependency.GetSortedResourceIds().ToArray());
        }
    }
}
