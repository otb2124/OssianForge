using OssianForge.Engine.Resources.Config;
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
            var tree = new Node();
            tree.Name = "tree";

            //script packs test
            /*
            var stateType = Engine.Resources.GetScriptType("filepack.script.ossian.stateMachine", "State");
            var GetDefaultName = stateType.GetMethod("GetDefaultName").Invoke(null, null);
            var StaticString = stateType.GetField("StaticString").GetValue(null);

            Console.WriteLine($"{stateType.Name}, GetDefaultName={GetDefaultName}, StaticString={StaticString}");

            var stateInstance = Engine.Resources.CreateScriptResourceInstance("filepack.script.ossian.stateMachine", "State");
            var StateName = stateInstance.GetType().GetField("Name").GetValue(stateInstance);

            Console.WriteLine($"{stateInstance.ToString()}, StateName={StateName}");
            */

            tree.AddChild(Engine.Resources.GetResourceFile<SceneConfig>("configfile.scene." + Engine.Resources.TreeConfig.MainScene).Scene);

            NodeManager.RegisterTree(tree);

            NodeManager.OnStart();
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
