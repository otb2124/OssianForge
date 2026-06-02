using System;
using OssianForge.Engine.Nodes.Models;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Nodes.Props
{
    public class StateProperty : NodeProperty
    {

        public State CurrentState;

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