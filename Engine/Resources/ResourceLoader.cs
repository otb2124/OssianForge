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
            ResourceFilesConfig = new ResourceFilesConfig("config.resourceFiles", "ConfigFiles/Core/resourceFiles.json");
            ResourcesConfig = new ResourcesConfig("config.resources", "ConfigFiles/Core/resources.json");
        }

        public void Initialize()
        {
            ResourceFilesConfig.Load();
            ResourceFilesConfig.BuildInstances();

            ResourcesConfig.Load();
            ResourcesConfig.BuildInstances();
        }

        public void OnLoad()
        {
            foreach (var resourceFile in ResourceFilesConfig.ResourceFiles)
            {
                resourceFile.Load();
            }

            foreach (var resource in ResourcesConfig.Resources)
            {
                resource.Load();
            }
        }
    }
}
