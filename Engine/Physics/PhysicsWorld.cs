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

        private readonly Dictionary<string, float> _coyoteTimers = new();
        private const float CoyoteTime = 0.15f;

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
                if (node.GetProperty<PhysicsProperty>().WorldIndex == WorldIndex)
                    Register(node);
            }
        }

        public PhysicsBody Register(Node node)
        {
            if (node == null) return null;
            if (_bodies.Any(b => b.NodeId == node.Id))
                return _bodies.First(b => b.NodeId == node.Id);

            PhysicsBody body = node.GetProperty<PhysicsProperty>().IsStatic
                ? new PhysicsStaticBody(node, JitterWorld)
                : new PhysicsRigidBody(node, JitterWorld);
            _bodies.Add(body);
            return body;
        }

        public void Unregister(Node node)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == node.Id);
            if (body == null) return;

            foreach (var c in body.Constraints)
                JitterWorld.Remove(c);

            if (body is PhysicsRigidBody rigid)
                JitterWorld.Remove(rigid.JitterBody);
            else
                foreach (var shape in body.OwnedShapes)
                    JitterWorld.NullBody.RemoveShape(shape);

            _bodies.RemoveAll(b => b.NodeId == node.Id);
        }

        public void OnUpdate(double delta)
        {
            float dt = Math.Clamp((float)delta, 0.001f, 0.033f);

            foreach (var key in _coyoteTimers.Keys.ToList())
                _coyoteTimers[key] -= dt;

            foreach (var body in _bodies.OfType<PhysicsRigidBody>())
                body.SyncToJitter();

            JitterWorld.Step(dt, multiThread: false);

            foreach (var body in _bodies.OfType<PhysicsRigidBody>())
                body.SyncFromJitter(LockPosition, LockRotation);
        }

        public T? GetBody<T>(string nodeId) where T : PhysicsBody =>
            _bodies.OfType<T>().FirstOrDefault(b => b.NodeId == nodeId);

        public bool IsGrounded(Node node, float maxGroundAngle = 90f)
        {
            var body = GetBody<PhysicsRigidBody>(node.Id);
            if (body?.JitterBody == null) return false;

            float cosThreshold = MathF.Cos(maxGroundAngle * MathF.PI / 180f);
            var up = new JVector(0, 1, 0);
            bool contacted = false;

            foreach (Arbiter arbiter in body.JitterBody.Contacts)
            {
                ref ContactData cd = ref arbiter.Handle.Data;

                JVector n0 = cd.Contact0.Normal;
                if (n0.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n0, up)) >= cosThreshold)
                { contacted = true; break; }

                JVector n1 = cd.Contact1.Normal;
                if (n1.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n1, up)) >= cosThreshold)
                { contacted = true; break; }

                JVector n2 = cd.Contact2.Normal;
                if (n2.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n2, up)) >= cosThreshold)
                { contacted = true; break; }

                JVector n3 = cd.Contact3.Normal;
                if (n3.LengthSquared() > 0.5f && MathF.Abs((float)JVector.Dot(n3, up)) >= cosThreshold)
                { contacted = true; break; }
            }

            string id = node.Id;

            if (contacted)
            {
                _coyoteTimers[id] = CoyoteTime;
                return true;
            }

            if (_coyoteTimers.TryGetValue(id, out float remaining) && remaining > 0f)
                return true;

            return false;
        }
    }
}