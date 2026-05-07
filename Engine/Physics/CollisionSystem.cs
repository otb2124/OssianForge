using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public static class CollisionSystem
    {
        public static void Process(List<Node> nodes)
        {
            // Flat list of all nodes with colliders
            var collidables = nodes
                .SelectMany(Flatten)
                .Where(n => n.GetProperty<ColliderProperty>() != null &&
                            n.GetProperty<TransformProperty>() != null)
                .ToList();

            for (int i = 0; i < collidables.Count; i++)
            {
                for (int j = i + 1; j < collidables.Count; j++)
                {
                    var a = collidables[i];
                    var b = collidables[j];

                    var colA = a.GetProperty<ColliderProperty>();
                    var colB = b.GetProperty<ColliderProperty>();
                    var tA = a.GetProperty<TransformProperty>();
                    var tB = b.GetProperty<TransformProperty>();

                    if (!colA.Intersects(colB, tA, tB)) continue;

                    // Fire callbacks
                    colA.OnCollision?.Invoke(b);
                    colB.OnCollision?.Invoke(a);

                    // Push apart if not triggers
                    if (!colA.IsTrigger && !colB.IsTrigger)
                    {
                        var push = colA.ResolveOverlap(colB, tA, tB);
                        tA.Transform.Position += push * 0.5f;
                        tB.Transform.Position -= push * 0.5f;
                    }
                }
            }
        }

        private static IEnumerable<Node> Flatten(Node node)
        {
            yield return node;
            foreach (var child in node.Children.SelectMany(Flatten))
                yield return child;
        }
    }
}