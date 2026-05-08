using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class CollisionSystem
    {
        public void OnUpdate(double delta)
        {
            var collidableNodes = Engine.Nodes.NodeManager
                .GetNodesWithProperties(typeof(TransformProperty), typeof(ColliderProperty));

            Console.WriteLine($"[Collision] Collidables found: {collidableNodes.Count} — " +
                             $"{string.Join(", ", collidableNodes.Select(n => n.Name ?? n.Id ?? "Unnamed"))}");

            Process(collidableNodes);
        }

        public void Process(List<Node> collidableNodes)
        {
            for (int i = 0; i < collidableNodes.Count; i++)
            {
                for (int j = i + 1; j < collidableNodes.Count; j++)
                {
                    Node nodeA = collidableNodes[i];
                    Node nodeB = collidableNodes[j];

                    var colA = nodeA.GetProperty<ColliderProperty>();
                    var colB = nodeB.GetProperty<ColliderProperty>();
                    var tA = nodeA.GetProperty<TransformProperty>();
                    var tB = nodeB.GetProperty<TransformProperty>();

                    // Safety check (should not happen due to GetNodesWithProperties)
                    if (colA == null || colB == null || tA == null || tB == null)
                        continue;

                    Console.WriteLine($"[Collision] Checking {nodeA.Name}({colA.GetType().Name}) vs {nodeB.Name}({colB.GetType().Name})");
                    Console.WriteLine($" posA={tA.Transform.Position} posB={tB.Transform.Position}");

                    bool intersects = colA.Intersects(colB, tA, tB);
                    Console.WriteLine($" Intersects: {intersects}");

                    if (!intersects) continue;

                    if (colA.IsTrigger || colB.IsTrigger)
                    {
                        Console.WriteLine($" Trigger — skipping resolution");
                        continue;
                    }

                    var push = colA.ResolveOverlap(colB, tA, tB);
                    Console.WriteLine($" Push vector: {push}");

                    var physA = nodeA.GetProperty<PhysicalProperty>();
                    var physB = nodeB.GetProperty<PhysicalProperty>();

                    bool staticA = physA?.IsStatic ?? true;
                    bool staticB = physB?.IsStatic ?? true;

                    Console.WriteLine($" staticA={staticA} staticB={staticB}");

                    if (staticA && staticB) continue;

                    if (staticA)
                    {
                        Console.WriteLine($" Pushing {nodeB.Name} by -{push}");
                        tB.Transform.Position -= push;
                        Engine.Physics.PhysicsWorld.ReflectVelocity(nodeB.Id, push);
                    }
                    else if (staticB)
                    {
                        Console.WriteLine($" Pushing {nodeA.Name} by +{push}");
                        tA.Transform.Position += push;
                        Engine.Physics.PhysicsWorld.ReflectVelocity(nodeA.Id, push);
                    }
                    else
                    {
                        tA.Transform.Position += push * 0.5f;
                        tB.Transform.Position -= push * 0.5f;
                        Engine.Physics.PhysicsWorld.ReflectVelocity(nodeA.Id, push);
                        Engine.Physics.PhysicsWorld.ReflectVelocity(nodeB.Id, -push);
                    }
                }
            }
        }
    }
}
