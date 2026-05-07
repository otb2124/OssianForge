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

        public PhysicsBody(Node node)
        {
            PhysicalProperty = node.GetProperty<PhysicalProperty>();
            ColliderProperty = node.GetProperty<ColliderProperty>();
            TransformProperty = node.GetProperty<TransformProperty>();
        }

        public void OnUpdate(float delta)
        {
            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            PhysicalProperty = node.GetProperty<PhysicalProperty>();
            ColliderProperty = node.GetProperty<ColliderProperty>();
            node.SetProperty(TransformProperty);
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