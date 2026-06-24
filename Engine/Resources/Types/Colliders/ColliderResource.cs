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

        public MeshResource _source;

        public ColliderResource(string id, string meshResourceId)
        {
            Id = id;
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            base.Load();
            Points.Clear();

            _source = Engine.Resources.GetResource<MeshResource>(MeshResourceId);

            var triangles = new List<JTriangle>();

            foreach (var sub in _source.SubMeshes)
            {
                int stride = 3;
                stride += 3;
                stride += 2;

                var v = sub.RawVertices;

                JVector Offset(int idx) => new JVector(
                    v[idx] - _source.HipsOffset.X,
                    v[idx + 1] - _source.HipsOffset.Y,
                    v[idx + 2] - _source.HipsOffset.Z);

                for (int i = 0; i + stride * 3 <= v.Length; i += stride * 3)
                {
                    var a = Offset(i);
                    var b = Offset(i + stride);
                    var c = Offset(i + stride * 2);
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