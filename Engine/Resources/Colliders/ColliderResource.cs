using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Meshes;

namespace OssianForge.Engine.Resources.Colliders
{
    public class ColliderResource : Resource, IDisposable
    {
        public string MeshResourceId;
        public TriangleMesh? TriangleMesh;
        public List<JVector> Points = new();

        private MeshResource _source;

        public ColliderResource(string id, string meshResourceId)
        {
            Id = id;
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            Points.Clear();

            _source = Engine.Resources.GetResource<MeshResource>(MeshResourceId);

            var triangles = new List<JTriangle>();

            foreach (var sub in _source.SubMeshes)
            {
                int stride = 3;
                stride += 3;
                stride += 2;

                var v = sub.RawVertices;
                for (int i = 0; i + stride * 3 <= v.Length; i += stride * 3)
                {
                    var a = new JVector(v[i], v[i + 1], v[i + 2]);
                    var b = new JVector(v[i + stride], v[i + stride + 1], v[i + stride + 2]);
                    var c = new JVector(v[i + stride * 2], v[i + stride * 2 + 1], v[i + stride * 2 + 2]);

                    triangles.Add(new JTriangle(a, b, c));
                    Points.Add(a);
                    Points.Add(b);
                    Points.Add(c);
                }
            }

            TriangleMesh = new TriangleMesh(triangles, ignoreDegenerated: true);
        }

        public void Dispose() { }
    }
}