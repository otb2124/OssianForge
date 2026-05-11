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

                    var bodyA = Engine.Physics.PhysicsWorld.GetBody(nodes[i].Id);
                    var bodyB = Engine.Physics.PhysicsWorld.GetBody(nodes[j].Id);
                    if (bodyA == null || bodyB == null) continue;

                    // Both static (NullBody) — skip, triggers need at least one dynamic
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