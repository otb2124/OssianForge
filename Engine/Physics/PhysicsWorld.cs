using Jitter2;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public enum AxisLock
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        All = X | Y | Z,
    }

    public class PhysicsWorld
    {
        public readonly World JitterWorld;
        public int WorldIndex;
        private readonly List<PhysicsBody> _bodies = new();

        public AxisLock LockPosition = AxisLock.None;
        public AxisLock LockRotation = AxisLock.None;

        public PhysicsWorld(int worldIndex, Vector3 gravity)
        {
            WorldIndex = worldIndex;
            JitterWorld = new World();
            JitterWorld.Gravity = new JVector(gravity.X, gravity.Y, gravity.Z);
        }

        public void RegisterAll()
        {
            _bodies.Clear();
            var nodes = Engine.Nodes.NodeManager.GetNodesWithProperty<PhysicalProperty>();
            foreach (var node in nodes)
            {
                // only register nodes that belong to this world
                if (node.GetProperty<PhysicalProperty>().WorldIndex == WorldIndex)
                    Register(node);
            }
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

            // Remove constraints first, before removing the body
            foreach (var c in body.Constraints)
                JitterWorld.Remove(c);

            if (body.JitterBody != null)
                JitterWorld.Remove(body.JitterBody);
            else
                foreach (var shape in body.OwnedShapes)
                    JitterWorld.NullBody.RemoveShape(shape);

            _bodies.RemoveAll(b => b.NodeId == node.Id);
        }

        public void OnUpdate(double delta)
        {
            float dt = Math.Clamp((float)delta, 0.001f, 0.033f);
            JitterWorld.Step(dt, multiThread: false);
            foreach (var body in _bodies)
            {
                if (WorldIndex == 1)
                {
                    var pos = body.JitterBody?.Position ?? default;
                    var vel = body.JitterBody?.Velocity ?? default;
                }
                body.SyncFromJitter(LockPosition, LockRotation);
            }
        }

        public PhysicsBody? GetBody(string nodeId) =>
            _bodies.FirstOrDefault(b => b.NodeId == nodeId);
    }
}