using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class CollisionSystem
    {
        public void OnUpdate(double delta)
        {
            var collidables = Engine.Nodes.NodeManager.Nodes
                .Where(n => n.GetProperty<ColliderProperty>() != null &&
                            n.GetProperty<TransformProperty>() != null)
                .Select(n => (
                    Collider: n.GetProperty<ColliderProperty>(),
                    Transform: n.GetProperty<TransformProperty>(),
                    Node: n
                ))
                .ToList();

            Console.WriteLine($"[Collision] Collidables found: {collidables.Count} — {string.Join(", ", collidables.Select(c => c.Node.Name))}");

            Process(collidables);
        }

        public void Process(List<(ColliderProperty Collider, TransformProperty Transform, Node Node)> collidables)
        {
            for (int i = 0; i < collidables.Count; i++)
            {
                for (int j = i + 1; j < collidables.Count; j++)
                {
                    var (colA, tA, nodeA) = collidables[i];
                    var (colB, tB, nodeB) = collidables[j];

                    Console.WriteLine($"[Collision] Checking {nodeA.Name}({colA.GetType().Name}) vs {nodeB.Name}({colB.GetType().Name})");
                    Console.WriteLine($"  posA={tA.Transform.Position} posB={tB.Transform.Position}");

                    bool intersects = colA.Intersects(colB, tA, tB);
                    Console.WriteLine($"  Intersects: {intersects}");

                    if (!intersects) continue;

                    if (colA.IsTrigger || colB.IsTrigger)
                    {
                        Console.WriteLine($"  Trigger — skipping resolution");
                        continue;
                    }

                    var push = colA.ResolveOverlap(colB, tA, tB);
                    Console.WriteLine($"  Push vector: {push}");

                    var physA = nodeA.GetProperty<PhysicalProperty>();
                    var physB = nodeB.GetProperty<PhysicalProperty>();
                    bool staticA = physA?.IsStatic ?? true;
                    bool staticB = physB?.IsStatic ?? true;
                    Console.WriteLine($"  staticA={staticA} staticB={staticB}");

                    if (staticA && staticB) continue;

                    if (staticA)
                    {
                        Console.WriteLine($"  Pushing {nodeB.Name} by -{push}");
                        tB.Transform.Position -= push;
                        Engine.Physics.PhysicsWorld.ReflectVelocity(nodeB.Id, push);
                    }
                    else if (staticB)
                    {
                        Console.WriteLine($"  Pushing {nodeA.Name} by +{push}");
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