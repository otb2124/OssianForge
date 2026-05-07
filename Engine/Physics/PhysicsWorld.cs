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

        public void Step(float delta)
        {
            // 1. Apply gravity and integrate velocity
            foreach (var body in _bodies)
            {
                if (body.PhysicalProperty.IsStatic) continue;

                if (body.PhysicalProperty.UseGravity)
                    body.PhysicalProperty.Velocity += Gravity * delta;

                body.TransformProperty.Transform.Position += body.PhysicalProperty.Velocity * delta;
            }

            // 2. Detect and resolve collisions
            for (int i = 0; i < _bodies.Count; i++)
            {
                for (int j = i + 1; j < _bodies.Count; j++)
                {
                    var a = _bodies[i];
                    var b = _bodies[j];

                    if (a.PhysicalProperty.IsStatic && b.PhysicalProperty.IsStatic) continue;

                    //NOTE: maybe should get dynamic updated one here
                    var colA = a.ColliderProperty;
                    var colB = b.ColliderProperty;
                    if (colA == null || colB == null) continue;

                    if (!colA.Intersects(colB, a.TransformProperty, b.TransformProperty)) continue;

                    // Fire triggers
                    //colA.OnCollision?.Invoke(b.Node);
                    //colB.OnCollision?.Invoke(a.Node);

                    if (colA.IsTrigger || colB.IsTrigger) continue;

                    // Resolve overlap
                    var push = colA.ResolveOverlap(colB, a.TransformProperty, b.TransformProperty);

                    if (a.PhysicalProperty.IsStatic)
                    {
                        b.TransformProperty.Transform.Position -= push;
                        ReflectVelocity(b, push);
                    }
                    else if (b.PhysicalProperty.IsStatic)
                    {
                        a.TransformProperty.Transform.Position += push;
                        ReflectVelocity(a, push);
                    }
                    else
                    {
                        a.TransformProperty.Transform.Position += push * 0.5f;
                        b.TransformProperty.Transform.Position -= push * 0.5f;
                        ReflectVelocity(a, push);
                        ReflectVelocity(b, -push);
                    }
                }
            }
        }

        private void ReflectVelocity(PhysicsBody body, Vector3 pushNormal)
        {
            if (pushNormal == Vector3.Zero) return;
            var normal = Vector3.Normalize(pushNormal);
            // Cancel velocity along collision normal, apply bounciness
            body.PhysicalProperty.Velocity -= normal * Vector3.Dot(body.PhysicalProperty.Velocity, normal) * (1f + body.PhysicalProperty.Bounciness);
        }
    }
}