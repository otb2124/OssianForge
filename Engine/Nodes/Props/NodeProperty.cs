using OssianForge.Engine.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class NodeProperty
    {


        public virtual void OnStart() { }

        public virtual void OnUpdate(Node node, double delta) { }

        public virtual void OnRender(Node node, double delta) { }
    }
}
