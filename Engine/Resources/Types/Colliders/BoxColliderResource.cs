using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;


namespace OssianForge.Engine.Resources.Colliders
{
    public class BoxColliderResource : ColliderResource
    {
        public Vector3 Size { get; private set; }

        public BoxColliderResource(string id, Vector3 size)
            : base(id)
        {
            Size = size;
            var half = size * 0.5f;
            AabbMin = new JVector(-half.X, -half.Y, -half.Z);
            AabbMax = new JVector(half.X, half.Y, half.Z);
        }

        public override void Load() { }

        public override RigidBodyShape CreateDynamicShape(Vector3 nodeScale)
        {
            var scaledSize = new JVector(
                Size.X * nodeScale.X,
                Size.Y * nodeScale.Y,
                Size.Z * nodeScale.Z);
            return new BoxShape(scaledSize);
        }

        public override IEnumerable<RigidBodyShape> CreateStaticShapes(Transform worldTransform)
            => Enumerable.Empty<RigidBodyShape>();

        public override SubMeshResource GetDebugMesh()
        {
            var half = Size * 0.5f;

            var verts = new List<float>();

            void Add(Vector3 p, Vector3 n, float u, float v)
            {
                verts.Add(p.X); verts.Add(p.Y); verts.Add(p.Z);
                verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
                verts.Add(u); verts.Add(v);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                Add(a, n, 0f, 0f); Add(b, n, 1f, 0f); Add(c, n, 1f, 1f);
                Add(a, n, 0f, 0f); Add(c, n, 1f, 1f); Add(d, n, 0f, 1f);
            }

            var x = half.X; var y = half.Y; var z = half.Z;

            // +X
            Quad(
                new Vector3(x, -y, -z), new Vector3(x, -y, z),
                new Vector3(x, y, z), new Vector3(x, y, -z),
                new Vector3(1, 0, 0));

            // -X
            Quad(
                new Vector3(-x, -y, z), new Vector3(-x, -y, -z),
                new Vector3(-x, y, -z), new Vector3(-x, y, z),
                new Vector3(-1, 0, 0));

            // +Y
            Quad(
                new Vector3(-x, y, -z), new Vector3(x, y, -z),
                new Vector3(x, y, z), new Vector3(-x, y, z),
                new Vector3(0, 1, 0));

            // -Y
            Quad(
                new Vector3(-x, -y, z), new Vector3(x, -y, z),
                new Vector3(x, -y, -z), new Vector3(-x, -y, -z),
                new Vector3(0, -1, 0));

            // +Z
            Quad(
                new Vector3(x, -y, z), new Vector3(-x, -y, z),
                new Vector3(-x, y, z), new Vector3(x, y, z),
                new Vector3(0, 0, 1));

            // -Z
            Quad(
                new Vector3(-x, -y, -z), new Vector3(x, -y, -z),
                new Vector3(x, y, -z), new Vector3(-x, y, -z),
                new Vector3(0, 0, -1));

            return new SubMeshResource(verts.ToArray(), 0);
        }
    }
}