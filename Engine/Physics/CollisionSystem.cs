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
            {
                for (int j = i + 1; j < collidableNodes.Count; j++)
                {
                    Node nodeA = collidableNodes[i];
                    Node nodeB = collidableNodes[j];

                    var colA = nodeA.GetProperty<ColliderProperty>();
                    var colB = nodeB.GetProperty<ColliderProperty>();
                    var tA = nodeA.GetProperty<TransformProperty>();
                    var tB = nodeB.GetProperty<TransformProperty>();

                    if (colA == null || colB == null || tA == null || tB == null)
                        continue;

                    bool intersects = colA.Intersects(colB, tA, tB);
                    if (!intersects) continue;

                    if (colA.IsTrigger || colB.IsTrigger)
                    {
                        continue;
                    }

                    var push = colA.ResolveOverlap(colB, tA, tB);

                    ResolvePhysicsResponse(nodeA, nodeB, push);
                }
            }
        }

        private void ResolvePhysicsResponse(Node nodeA, Node nodeB, Vector3 push)
        {
            var physA = nodeA.GetProperty<PhysicalProperty>();
            var physB = nodeB.GetProperty<PhysicalProperty>();
            bool staticA = physA?.IsStatic ?? true;
            bool staticB = physB?.IsStatic ?? true;

            if (staticA && staticB) return;
            if (push == Vector3.Zero) return;

            var tA = nodeA.GetProperty<TransformProperty>();
            var tB = nodeB.GetProperty<TransformProperty>();

            if (staticA)
            {
                // push already points in the direction to move B away from A
                tB.Transform.Position += push;
                Engine.Physics.PhysicsWorld.ReflectVelocity(nodeB.Id, push);
                if (push.Y > 0)
                {
                    Engine.Physics.PhysicsWorld.SetGrounded(nodeB.Id, true, isOnStatic: true);
                    Engine.Physics.PhysicsWorld.SetGroundedY(nodeB.Id, tB.Transform.Position.Y);
                }
            }
            else if (staticB)
            {
                // push points away from A toward B, so A needs to go opposite
                tA.Transform.Position -= push;
                Engine.Physics.PhysicsWorld.ReflectVelocity(nodeA.Id, -push);
                if (push.Y < 0)
                {
                    Engine.Physics.PhysicsWorld.SetGrounded(nodeA.Id, true, isOnStatic: true);
                    Engine.Physics.PhysicsWorld.SetGroundedY(nodeA.Id, tA.Transform.Position.Y);
                }
            }
            else
            {
                tA.Transform.Position -= push * 0.5f;
                tB.Transform.Position += push * 0.5f;
                Engine.Physics.PhysicsWorld.ReflectVelocity(nodeA.Id, -push);
                Engine.Physics.PhysicsWorld.ReflectVelocity(nodeB.Id, push);

                if (push.Y > 0)
                {
                    // B is above A — B is resting on A
                    Engine.Physics.PhysicsWorld.SetGrounded(nodeB.Id, true, isOnStatic: false);
                    Engine.Physics.PhysicsWorld.SetGroundedY(nodeB.Id, tB.Transform.Position.Y);
                }
                if (push.Y < 0)
                {
                    // A is above B — A is resting on B
                    Engine.Physics.PhysicsWorld.SetGrounded(nodeA.Id, true, isOnStatic: false);
                    Engine.Physics.PhysicsWorld.SetGroundedY(nodeA.Id, tA.Transform.Position.Y);
                }
            }
        }
    }
}

