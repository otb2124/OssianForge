using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;


namespace OssianForge.Engine.Physics
{
    public class CollisionSystem
    {
        public void OnUpdate(double delta)
        {
            var nodes = Engine.Nodes.NodeManager
                .GetNodesWithProperties(typeof(TransformProperty), typeof(ColliderProperty));
            CheckTriggers(nodes);
        }
        private void CheckTriggers(List<Node> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var colA = nodes[i].GetProperty<ColliderProperty>();
                    var colB = nodes[j].GetProperty<ColliderProperty>();
                    if (colA == null || colB == null) continue;
                    if (!colA.IsTrigger && !colB.IsTrigger) continue;
                    // must be in the same world to interact
                    var physA = nodes[i].GetProperty<PhysicalProperty>();
                    var physB = nodes[j].GetProperty<PhysicalProperty>();
                    if (physA == null || physB == null) continue;
                    if (physA.WorldIndex != physB.WorldIndex) continue;
                    var world = Engine.Physics.GetWorld(physA.WorldIndex);
                    var bodyA = world.GetBody(nodes[i].Id);
                    var bodyB = world.GetBody(nodes[j].Id);
                    if (bodyA == null || bodyB == null) continue;
                    if (bodyA.JitterBody == null && bodyB.JitterBody == null) continue;
                    var posA = bodyA.JitterBody?.Position ?? default;
                    var posB = bodyB.JitterBody?.Position ?? default;
                    if ((posA - posB).Length() < 2f)
                    {
                        colA.OnCollision?.Invoke(nodes[j]);
                        colB.OnCollision?.Invoke(nodes[i]);
                    }
                }
        }
    }
}