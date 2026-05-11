using Jitter2;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsWorld
    {
        public readonly World JitterWorld;
        private readonly List<PhysicsBody> _bodies = new();

        public PhysicsWorld()
        {
            JitterWorld = new World();
            JitterWorld.Gravity = new JVector(0, -9.81f, 0);
        }

        public void RegisterAll()
        {
            _bodies.Clear();
            var nodes = Engine.Nodes.NodeManager.GetNodesWithProperty<PhysicalProperty>();
            foreach (var node in nodes)
                Register(node);
        }

        public PhysicsBody Register(Node node)
        {
            if (node == null) return null;
            if (_bodies.Any(b => b.NodeId == node.Id))
                return _bodies.First(b => b.NodeId == node.Id);

            var body = new PhysicsBody(node, JitterWorld);
            _bodies.Add(body);
            return body;
        }

        public void Unregister(Node node)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == node.Id);
            if (body == null) return;

            if (body.JitterBody != null)
            {
                JitterWorld.Remove(body.JitterBody);
            }
            else
            {
                // Static: remove triangle shapes from NullBody
                foreach (var shape in body.OwnedShapes)
                    JitterWorld.NullBody.RemoveShape(shape);
            }

            _bodies.RemoveAll(b => b.NodeId == node.Id);
        }

        public void OnUpdate(double delta)
        {
            float dt = Math.Clamp((float)delta, 0.001f, 0.033f);
            JitterWorld.Step(dt, multiThread: false);

            foreach (var body in _bodies)
                body.SyncFromJitter();
        }

        // Kept so CollisionSystem and any external callers still compile
        public void ResolveCollision(Node nodeA, Node nodeB, Vector3 push) { }
        public void ResetGrounded() { }

        public bool IsGrounded(string nodeId)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body?.JitterBody == null) return false;
            return MathF.Abs(body.JitterBody.Velocity.Y) < 0.1f;
        }

        public PhysicsBody? GetBody(string nodeId) =>
            _bodies.FirstOrDefault(b => b.NodeId == nodeId);


    }
}