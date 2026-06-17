using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    public class ResourceLoadScheduler
    {


        public ResourceLoadScheduler()
        {

        }


        public void Initialize()
        {
            var sceneConfig = Engine.Resources.GetResourceFile<SceneConfig>("configfile.scene.main");
            Console.WriteLine($"[RESOURCE SCHEDULER] Extracted {sceneConfig.GetDependency().ResourceIds.Count} resources from scene config {sceneConfig.Id}");
        }
    }
}
