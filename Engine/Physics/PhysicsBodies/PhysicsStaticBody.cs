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

            var transformProperty = node.GetProperty<TransformProperty>();
            var colliderProperty = node.GetProperty<ColliderProperty>();
            var physicsProperty = node.GetProperty<PhysicsProperty>();

            var t = transformProperty.WorldTransform;
            var sourceMesh = colliderProperty.ColliderResource.TriangleMesh!;
            var transformed = new List<JTriangle>();

            for (int i = 0; i < sourceMesh.Indices.Length; i++)
            {
                var idx = sourceMesh.Indices[i];
                var a = sourceMesh.Vertices[idx.IndexA];
                var b = sourceMesh.Vertices[idx.IndexB];
                var c = sourceMesh.Vertices[idx.IndexC];

                a = TransformJVertex(a, t);
                b = TransformJVertex(b, t);
                c = TransformJVertex(c, t);

                transformed.Add(new JTriangle(a, b, c));
            }

            var positionedMesh = new TriangleMesh(transformed, ignoreDegenerated: true);

            foreach (var shape in TriangleShape.CreateAllShapes(positionedMesh))
            {
                Engine.Physics.GetWorld(physicsProperty.WorldIndex).JitterWorld.NullBody.AddShape(shape, setMassInertia: false);
                OwnedShapes.Add(shape);
            }
        }
    }
}