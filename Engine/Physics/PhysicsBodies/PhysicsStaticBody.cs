using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;

namespace OssianForge.Engine.Physics
{
    public class PhysicsStaticBody : PhysicsBody
    {
        public PhysicsStaticBody(Node node, World jitterWorld) : base(node)
        {
            var t = TransformProperty.WorldTransform;
            var sourceMesh = ColliderProperty.ColliderResource.TriangleMesh!;
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
                jitterWorld.NullBody.AddShape(shape, setMassInertia: false);
                OwnedShapes.Add(shape);
            }
        }
    }
}