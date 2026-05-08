using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsWorld
    {
        public Vector3 Gravity = new Vector3(0, -9.81f, 0);

        private readonly List<PhysicsBody> _bodies = new();

        public PhysicsBody Register(Node node)
        {
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

        public void Step(float delta)
        {
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;
                if (body.PhysicalProperty.UseGravity)
                    body.PhysicalProperty.Velocity += Gravity * delta;
                body.TransformProperty.Transform.Position += body.PhysicalProperty.Velocity * delta;

                Console.WriteLine($"[Physics] {body.NodeId} pos={body.TransformProperty.Transform.Position} vel={body.PhysicalProperty.Velocity}");
            }
        }

        public void ReflectVelocity(string nodeId, Vector3 pushNormal)
        {
            var body = _bodies.FirstOrDefault(b => b.NodeId == nodeId);
            if (body == null || pushNormal == Vector3.Zero) return;
            var normal = Vector3.Normalize(pushNormal);
            body.PhysicalProperty.Velocity -= normal * Vector3.Dot(body.PhysicalProperty.Velocity, normal) * (1f + body.PhysicalProperty.Bounciness);
        }
    }
}