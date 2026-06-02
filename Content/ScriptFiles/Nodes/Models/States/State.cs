using OssianForge.Engine.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Models
{

    public class State
    {
        public virtual void OnUpdate(Node node, double delta) {}

        public virtual void OnRender(Node node, double delta) { }
    }
}
