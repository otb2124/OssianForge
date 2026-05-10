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
            Process(collidableNodes);
        }

        public void Process(List<Node> collidableNodes)
        {
            for (int i = 0; i < collidableNodes.Count; i++)
                for (int j = i + 1; j < collidableNodes.Count; j++)
                {
                    var nodeA = collidableNodes[i];
                    var nodeB = collidableNodes[j];

                    var colA = nodeA.GetProperty<ColliderProperty>();
                    var colB = nodeB.GetProperty<ColliderProperty>();
                    var tA = nodeA.GetProperty<TransformProperty>();
                    var tB = nodeB.GetProperty<TransformProperty>();

                    if (colA == null || colB == null || tA == null || tB == null) continue;
                    if (!colA.Intersects(colB, tA, tB)) continue;
                    if (colA.IsTrigger || colB.IsTrigger) continue;

                    var push = colA.ResolveOverlap(colB, tA, tB);

                    Engine.Physics.PhysicsWorld.ResolveCollision(nodeA, nodeB, push);
                }
        }
    }
}