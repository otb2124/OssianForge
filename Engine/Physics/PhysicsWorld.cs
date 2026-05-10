using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsWorld
    {
        public Vector3 Gravity = new Vector3(0, -9.81f, 0);
        private readonly List<PhysicsBody> _bodies = new();

        // ----------------------------------------------------------------
        // Registration
        // ----------------------------------------------------------------

        public void RegisterAll()
        {
            var physicsNodes = Engine.Nodes.NodeManager.GetNodesWithProperty<PhysicalProperty>();
            _bodies.Clear();
            foreach (var node in physicsNodes)
                Register(node);
        }

        public PhysicsBody Register(Node node)
        {
            if (node == null) return null;
            if (_bodies.Any(b => b.NodeId == node.Id))
                return _bodies.First(b => b.NodeId == node.Id);
            var body = new PhysicsBody(node);
            _bodies.Add(body);
            return body;
        }

        public void Unregister(Node node) =>
            _bodies.RemoveAll(b => b.NodeId == node.Id);

        // ----------------------------------------------------------------
        // Per-frame step
        // ----------------------------------------------------------------

        public void OnUpdate(double delta) => Step((float)delta);

        public void Step(float delta)
        {
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;

                if (body.PhysicalProperty.Velocity.Y > 0.1f)
                {
                    body.IsGrounded = false;
                    body.GroundedY = float.MinValue;
                }

                if (body.IsGrounded && body.GroundedY > float.MinValue)
                {
                    body.PhysicalProperty.Velocity = new Vector3(
                        body.PhysicalProperty.Velocity.X, 0f,
                        body.PhysicalProperty.Velocity.Z);
                }
                else
                {
                    if (body.PhysicalProperty.UseGravity)
                        body.PhysicalProperty.Velocity += Gravity * delta;
                }

                body.PhysicalProperty.Velocity *= 0.98f;

                if (body.PhysicalProperty.Velocity.LengthSquared() < 0.005f)
                    body.PhysicalProperty.Velocity = Vector3.Zero;

                body.TransformProperty.Transform.Position += new Vector3(
                    body.PhysicalProperty.Velocity.X,
                    body.IsGrounded ? 0f : body.PhysicalProperty.Velocity.Y,
                    body.PhysicalProperty.Velocity.Z) * delta;

                if (body.GroundedY > float.MinValue &&
                    body.TransformProperty.Transform.Position.Y < body.GroundedY)
                {
                    body.TransformProperty.Transform.Position = new Vector3(
                        body.TransformProperty.Transform.Position.X,
                        body.GroundedY,
                        body.TransformProperty.Transform.Position.Z);
                    if (body.PhysicalProperty.Velocity.Y < 0)
                        body.PhysicalProperty.Velocity = new Vector3(
                            body.PhysicalProperty.Velocity.X, 0,
                            body.PhysicalProperty.Velocity.Z);
                }

                if (body.PhysicalProperty.Velocity.Y > 0.5f)
                    body.GroundedY = float.MinValue;
            }
        }

        // ----------------------------------------------------------------
        // Collision response — called by CollisionSystem
        // ----------------------------------------------------------------

        public void ResolveCollision(Node nodeA, Node nodeB, Vector3 push)
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
                tB.Transform.Position += push;
                ReflectVelocity(nodeB.Id, push, againstStatic: true);
                if (push.Y > 0)
                {
                    SetGrounded(nodeB.Id, true, isOnStatic: true);
                    SetGroundedY(nodeB.Id, tB.Transform.Position.Y);
                }
            }
            else if (staticB)
            {
                tA.Transform.Position -= push;
                ReflectVelocity(nodeA.Id, -push, againstStatic: true);
                if (push.Y < 0)
                {
                    SetGrounded(nodeA.Id, true, isOnStatic: true);
                    SetGroundedY(nodeA.Id, tA.Transform.Position.Y);
                }
            }
            else
            {
                tA.Transform.Position -= push * 0.5f;
                tB.Transform.Position += push * 0.5f;
                ReflectVelocity(nodeA.Id, -push, againstStatic: false);
                ReflectVelocity(nodeB.Id, push, againstStatic: false);

                if (push.Y > 0)
                {
                    SetGrounded(nodeB.Id, true, isOnStatic: false);
                    SetGroundedY(nodeB.Id, tB.Transform.Position.Y);
                }
                if (push.Y < 0)
                {
                    SetGrounded(nodeA.Id, true, isOnStatic: false);
                    SetGroundedY(nodeA.Id, tA.Transform.Position.Y);
                }
            }
        }

        // ----------------------------------------------------------------
        // Grounded state
        // ----------------------------------------------------------------

        public void SetGrounded(string nodeId, bool grounded, bool isOnStatic = false)
        {
            var body = GetBody(nodeId);
            if (body == null) return;
            body.IsGrounded = grounded;
            if (!isOnStatic) body.GroundedY = float.MinValue;
        }

        public void SetGroundedY(string nodeId, float y)
        {
            var body = GetBody(nodeId);
            if (body != null) body.GroundedY = y;
        }

        public bool IsGrounded(string nodeId) =>
            GetBody(nodeId)?.IsGrounded ?? false;

        public void ResetGrounded()
        {
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;
                body.IsGrounded = false;
            }
        }

        // ----------------------------------------------------------------
        // Velocity
        // ----------------------------------------------------------------

        public void ReflectVelocity(string nodeId, Vector3 pushNormal, bool againstStatic = false)
        {
            var body = GetBody(nodeId);
            if (body == null || pushNormal == Vector3.Zero) return;

            var normal = Vector3.Normalize(pushNormal);
            float relVel = Vector3.Dot(body.PhysicalProperty.Velocity, normal);

            if (relVel >= 0) return;

            if (againstStatic && MathF.Abs(relVel) < 0.5f)
            {
                body.PhysicalProperty.Velocity -= normal * relVel;
                return;
            }

            body.PhysicalProperty.Velocity -= normal * relVel * (1f + body.PhysicalProperty.Bounciness);
        }

        public void ZeroDownwardVelocity(string nodeId)
        {
            var body = GetBody(nodeId);
            if (body == null || body.PhysicalProperty.Velocity.Y >= 0) return;
            body.PhysicalProperty.Velocity = new Vector3(
                body.PhysicalProperty.Velocity.X, 0,
                body.PhysicalProperty.Velocity.Z);
        }

        // ----------------------------------------------------------------
        // Private
        // ----------------------------------------------------------------

        private PhysicsBody? GetBody(string nodeId) =>
            _bodies.FirstOrDefault(b => b.NodeId == nodeId);
    }
}