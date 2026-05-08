using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Physics
{
    public class PhysicsBody
    {
        public string NodeId;

        public PhysicalProperty PhysicalProperty;
        public ColliderProperty ColliderProperty;
        public TransformProperty TransformProperty;

        public bool IsGrounded;
        public float GroundedY = float.MinValue;

        public PhysicsBody(Node node)
        {
            NodeId = node.Id;
            PhysicalProperty = node.GetProperty<PhysicalProperty>();
            ColliderProperty = node.GetProperty<ColliderProperty>();
            TransformProperty = node.GetProperty<TransformProperty>();
        }

        public void AddForce(Vector3 force)
        {
            if (!PhysicalProperty.IsStatic)
                PhysicalProperty.Velocity += force / PhysicalProperty.Mass;
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (!PhysicalProperty.IsStatic)
                PhysicalProperty.Velocity += impulse;
        }
    }
}