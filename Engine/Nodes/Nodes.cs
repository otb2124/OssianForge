using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes
{
    public class Nodes
    {

        public NodeManager NodeManager;

        public Nodes() { NodeManager = new NodeManager(); }


        public void Initialize()
        {
            NodeManager.Initialize();
        }

        public void OnLoad()
        {
            NodeManager.RegisterTree(NodeTree.GetTree());
        }
        public void OnUpdate(double delta)
        { 
            NodeManager.UpdateNodes(delta);
        }

        public void OnRender(double delta)
        {
            NodeManager.RenderNodes(delta); 
        }
    }
}
