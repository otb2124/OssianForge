using Jitter2.LinearMath;
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
                    var nodeA = nodes[i];
                    var nodeB = nodes[j];

                    var colA = nodeA.GetProperty<ColliderProperty>();
                    var colB = nodeB.GetProperty<ColliderProperty>();
                    if (colA == null || colB == null) continue;
                    if (!colA.IsTrigger && !colB.IsTrigger) continue;

                    var physA = nodeA.GetProperty<PhysicalProperty>();
                    var physB = nodeB.GetProperty<PhysicalProperty>();
                    if (physA == null || physB == null) continue;
                    if (physA.WorldIndex != physB.WorldIndex) continue;

                    var world = Engine.Physics.GetWorld(physA.WorldIndex);
                    var bodyA = world.GetBody(nodeA.Id);
                    var bodyB = world.GetBody(nodeB.Id);
                    if (bodyA == null || bodyB == null) continue;

                    var transA = nodeA.GetProperty<TransformProperty>();
                    var transB = nodeB.GetProperty<TransformProperty>();

                    var posA = bodyA.JitterBody?.Position
                        ?? new JVector(transA.Transform.Position.X, transA.Transform.Position.Y, transA.Transform.Position.Z);
                    var posB = bodyB.JitterBody?.Position
                        ?? new JVector(transB.Transform.Position.X, transB.Transform.Position.Y, transB.Transform.Position.Z);

                    // Use the actual half-extents from each collider's scale
                    var scaleA = transA.Transform.Scale;
                    var scaleB = transB.Transform.Scale;
                    float radiusA = Math.Max(scaleA.X, scaleA.Y) * 0.5f;
                    float radiusB = Math.Max(scaleB.X, scaleB.Y) * 0.5f;

                    var dist = (posA - posB).Length();
                    float combinedRadius = radiusA + radiusB;

                    if (physA.WorldIndex == 1)  // log world 1 pairs only
                        Console.WriteLine($"[W1 TRIGGER] {nodeA.Name} <-> {nodeB.Name} | dist={dist:F1} combined={combinedRadius:F1} overlap={dist < combinedRadius}");

                    if (dist < combinedRadius)
                    {
                        Console.WriteLine($"[W1 COLLISION] {nodeA.Name} HIT {nodeB.Name}");
                        colA.OnCollision?.Invoke(nodeB);
                        colB.OnCollision?.Invoke(nodeA);
                    }
                }
        }
    }
}