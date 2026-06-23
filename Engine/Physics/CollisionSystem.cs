using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;


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

                    var physA = nodeA.GetProperty<PhysicsProperty>();
                    var physB = nodeB.GetProperty<PhysicsProperty>();
                    if (physA == null || physB == null) continue;
                    if (physA.WorldIndex != physB.WorldIndex) continue;

                    var transA = nodeA.GetProperty<TransformProperty>();
                    var transB = nodeB.GetProperty<TransformProperty>();

                    var (minA, maxA) = GetNodeBounds(nodeA, transA);
                    var (minB, maxB) = GetNodeBounds(nodeB, transB);

                    bool overlaps =
                        minA.X <= maxB.X && maxA.X >= minB.X &&
                        minA.Y <= maxB.Y && maxA.Y >= minB.Y &&
                        minA.Z <= maxB.Z && maxA.Z >= minB.Z;

                    if (overlaps)
                    {
                        colA.OnCollision?.Invoke(nodeB);
                        colB.OnCollision?.Invoke(nodeA);
                    }
                }
        }

        private static (Vector3 min, Vector3 max) GetNodeBounds(Node node, TransformProperty trans)
        {
            var animSourceNode = node.GetProperty<ColliderProperty>()?.AnimationSourceNodeId != null
                ? Engine.Nodes.NodeManager.GetNode(node.GetProperty<ColliderProperty>().AnimationSourceNodeId)
                : node;

            var anim = animSourceNode?.GetProperty<AnimationProperty>();
            var worldMatrix = trans.WorldTransform.ToMatrix();

            if (anim != null && anim.BonePalette.Length > 0)
                return anim.GetAnimatedWorldBounds(worldMatrix);

            // Static fallback — use collider source mesh local AABB transformed to world
            var mesh = node.GetProperty<ColliderProperty>()?.ColliderResource._source;
            if (mesh != null)
            {
                var wMin = Vector3.Transform(mesh.LocalAabbMin, worldMatrix);
                var wMax = Vector3.Transform(mesh.LocalAabbMax, worldMatrix);
                return (Vector3.Min(wMin, wMax), Vector3.Max(wMin, wMax));
            }

            // Last resort — point
            var p = trans.WorldTransform.Position;
            return (p, p);
        }
    }
}