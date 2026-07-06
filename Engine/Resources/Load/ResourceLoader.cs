using OssianForge.Engine.Resources.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            ResourcesConfig.Load();

            ResourcesConfig.BuildInstances<TreeConfig>();
            ResourcesConfig.LoadResources<TreeConfig>();

            ResourcesConfig.BuildInstances<SceneConfig>();
            ResourcesConfig.LoadResources<SceneConfig>(); 
        }

        public void InitializeResources()
        {
            ResourcesConfig.BuildInstances(Engine.Resources.ResourceScheduler.NodeDependency.GetSortedResourceIds().ToArray());
        }

        public void OnLoad()
        {
            ResourcesConfig.LoadResources(Engine.Resources.ResourceScheduler.NodeDependency.GetSortedResourceIds().ToArray());
        }
    }
}
