using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Collision;
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
            var nodes = Engine.Nodes.NodeManager.GetNodesWithProperty<PhysicsProperty>();
            foreach (var node in nodes)
            {
                // only register nodes that belong to this world
                if (node.GetProperty<PhysicsProperty>().WorldIndex == WorldIndex)
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

            foreach (var body in _bodies)
                body.SyncToJitter();

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


        public bool IsGrounded(Node node, float maxGroundAngle = 46f)
        {
            var body = GetBody(node.Id);
            if (body?.JitterBody == null) return false;

            float cosThreshold = MathF.Cos(maxGroundAngle * MathF.PI / 180f);
            var up = new JVector(0, 1, 0);

            foreach (Arbiter arbiter in body.JitterBody.Contacts)
            {
                ref ContactData cd = ref arbiter.Handle.Data;

                JVector n0 = cd.Contact0.Normal;
                if (n0.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n0, up)) >= cosThreshold)
                    return true;

                JVector n1 = cd.Contact1.Normal;
                if (n1.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n1, up)) >= cosThreshold)
                    return true;

                JVector n2 = cd.Contact2.Normal;
                if (n2.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n2, up)) >= cosThreshold)
                    return true;

                JVector n3 = cd.Contact3.Normal;
                if (n3.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n3, up)) >= cosThreshold)
                    return true;
            }

            return false;
        }
    }
}