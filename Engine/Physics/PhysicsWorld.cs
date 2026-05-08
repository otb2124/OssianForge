using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsWorld
    {
        public Vector3 Gravity = new Vector3(0, -9.81f, 0);

        private readonly List<PhysicsBody> _bodies = new();


        public void RegisterAll()
        {
            var physicsNodes = Engine.Nodes.NodeManager.GetNodesWithProperty<PhysicalProperty>();

            _bodies.Clear();

            foreach (var node in physicsNodes)
            {
                Register(node);
            }

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

        public void Unregister(Node node)
        {
            _bodies.RemoveAll(b => b.NodeId == node.Id);
        }

        public void OnUpdate(double delta)
        {
            Step((float)delta);
        }

        public void SetGrounded(string nodeId, bool grounded, bool isOnStatic = false)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body == null) return;
            body.IsGrounded = grounded;
            // Only pin GroundedY when resting on static geometry
            if (!isOnStatic) body.GroundedY = float.MinValue;
        }

        public void ResetGrounded()
        {
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;

                // Only clear grounded if moving upward — still grounded if stationary or falling
                if (body.PhysicalProperty.Velocity.Y > 0.1f)
                {
                    body.IsGrounded = false;
                    body.GroundedY = float.MinValue;
                }
                else
                {
                    // Keep grounded state but let collision re-confirm it
                    body.IsGrounded = false;
                    // Keep GroundedY — the pin stays active even if collision doesn't fire this frame
                }
            }
        }

        public bool IsGrounded(string nodeId)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            return body?.IsGrounded ?? false;
        }

        public void SetGroundedY(string nodeId, float y)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body != null) body.GroundedY = y;
        }

        public void Step(float delta)
        {
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;

                // If we just bounced, the upward velocity should clear grounded immediately
                if (body.PhysicalProperty.Velocity.Y > 0.1f)
                {
                    body.IsGrounded = false;
                    body.GroundedY = float.MinValue;
                }

                if (body.IsGrounded && body.GroundedY > float.MinValue)
                {
                    body.PhysicalProperty.Velocity = new Vector3(
                        body.PhysicalProperty.Velocity.X,
                        0f,
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

                // Hard floor — never sink below last known ground contact
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

                // Clear GroundedY if object moves significantly upward (jumped/launched)
                if (body.PhysicalProperty.Velocity.Y > 0.5f)
                    body.GroundedY = float.MinValue;
            }
        }

        public void ReflectVelocity(string nodeId, Vector3 pushNormal)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body == null || pushNormal == Vector3.Zero) return;

            var normal = Vector3.Normalize(pushNormal);
            float relVel = Vector3.Dot(body.PhysicalProperty.Velocity, normal);

            Console.WriteLine($"[ReflectVelocity] {nodeId} relVel={relVel:F3} bounciness={body.PhysicalProperty.Bounciness}");

            if (relVel >= 0) return;

            const float restingThreshold = 0.5f;
            if (MathF.Abs(relVel) < restingThreshold)
            {
                Console.WriteLine($"[ReflectVelocity] {nodeId} resting contact, zeroing");
                body.PhysicalProperty.Velocity -= normal * relVel;
                return;
            }

            float restitution = body.PhysicalProperty.Bounciness;
            body.PhysicalProperty.Velocity -= normal * relVel * (1f + restitution);
            Console.WriteLine($"[ReflectVelocity] {nodeId} bounced, new vel={body.PhysicalProperty.Velocity}");
        }

        public void ZeroDownwardVelocity(string nodeId)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body == null) return;
            if (body.PhysicalProperty.Velocity.Y < 0)
                body.PhysicalProperty.Velocity = new Vector3(
                    body.PhysicalProperty.Velocity.X,
                    0,
                    body.PhysicalProperty.Velocity.Z);
        }
    }
}