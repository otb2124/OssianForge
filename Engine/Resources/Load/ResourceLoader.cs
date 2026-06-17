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

        public ResourceLoadScheduler ResourceLoadScheduler;

        public ResourceFilesConfig ResourceFilesConfig;
        public ResourcesConfig ResourcesConfig;

        public ResourceLoader() 
        {
            ResourceLoadScheduler = new ResourceLoadScheduler();
            ResourceFilesConfig = new ResourceFilesConfig("configfile.resourceFiles", "ConfigFiles/Core/resourceFiles.json");
            ResourcesConfig = new ResourcesConfig("configfile.resources", "ConfigFiles/Core/resources.json");
        }

        public void Initialize()
        {
            ResourceFilesConfig.Load();
            ResourceFilesConfig.BuildInstances<SceneConfig>();
            ResourceFilesConfig.LoadResourceFiles<SceneConfig>();

            ResourceLoadScheduler.Initialize();

            ResourceFilesConfig.BuildInstances();

            ResourcesConfig.Load();
            ResourcesConfig.BuildInstances();
        }

        public void OnLoad()
        {
            ResourceFilesConfig.LoadResourceFiles();
            ResourcesConfig.LoadResources();
        }
    }
}
