using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    public class ResourceScheduler
    {

        public NodeDependency NodeDependency;

        public ResourceScheduler()
        {
            NodeDependency = new NodeDependency();
        }

        public void Initialize()
        {
            NodeDependency.ExtractTree("configfile.tree");
            NodeDependency.ExtractScene(Engine.Resources.GetResourceFile<TreeConfig>("configfile.tree").MainScene);
            NodeDependency.ExtractScene("configfile.scene.player");
            NodeDependency.ExtractScene("configfile.scene.debugui");
        }
    }
}
