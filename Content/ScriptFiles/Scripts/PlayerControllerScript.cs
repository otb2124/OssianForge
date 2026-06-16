using OssianForge.Engine.Nodes;
using System;

public class CustomNodesScript
{
    public void OnStart(Node node)
    {
        Console.WriteLine($"[SCRIPT] CustomNodesScript started on '{node.Name}'");
    }

    public void OnUpdate(Node node, double delta)
    {
        // your per-frame logic here
    }
}