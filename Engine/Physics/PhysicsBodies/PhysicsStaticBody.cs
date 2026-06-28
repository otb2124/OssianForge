using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Physics
{
    public class PhysicsStaticBody : PhysicsBody
    {
        public PhysicsStaticBody() : base()
        {
            
        }

        public override void Init(Node node)
        {
            base.Init(node);

            var colliderProperty = node.GetProperty<ColliderProperty>();
            var transformProperty = node.GetProperty<TransformProperty>();

            foreach (var shape in colliderProperty.ColliderResource.CreateStaticShapes(transformProperty.WorldTransform))
            {
                Engine.Physics.GetWorld(0).JitterWorld.NullBody.AddShape(shape, setMassInertia: false);
                OwnedShapes.Add(shape);
            }
        }
    }
}