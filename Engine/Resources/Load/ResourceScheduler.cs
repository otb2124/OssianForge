using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    //TODO: deprecate
    public class ResourceScheduler
    {

        public NodeDependency NodeDependency;

        public ResourceScheduler()
        {
            NodeDependency = new NodeDependency();
        }

        public void Initialize()
        {
        }
    }
}
