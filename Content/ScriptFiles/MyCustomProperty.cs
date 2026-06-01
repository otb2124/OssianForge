using System;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Nodes.Props
{
    public class MyCustomProperty : NodeProperty
    {

        public void OnUpdate(float deltaTime)
        {
            Console.WriteLine("custom property update");
        }
    }
}