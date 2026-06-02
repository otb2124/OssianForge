using System;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes;

namespace OssianForge.Engine.Nodes.Props
{
    public class InputControlProperty : NodeProperty
    {


        public override void OnStart()
        {
            //Console.WriteLine("custom property start");
        }


        public override void OnUpdate(Node node, double delta)
        {
            Console.WriteLine("custom property update");
        }
    }
}