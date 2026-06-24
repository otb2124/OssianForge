using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Colliders
{
    
    public class ColliderResource : Resource, IDisposable
    {
        public string MeshResourceId;
        public TriangleMesh? TriangleMesh;
        public List<JVector> Points = new();
        public JVector AabbMin;
        public JVector AabbMax;

        public ColliderResource(string id, string meshResourceId)
        {
            Id = id;
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            base.Load();
            Points.Clear();
            var _source = Engine.Resources.GetResource<MeshResource>(MeshResourceId);
            var triangles = new List<JTriangle>();

            var min = new JVector(float.MaxValue);
            var max = new JVector(float.MinValue);

            foreach (var sub in _source.SubMeshes)
            {
                int stride = 8;
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

                    min = JVector.Min(min, JVector.Min(a, JVector.Min(b, c)));
                    max = JVector.Max(max, JVector.Max(a, JVector.Max(b, c)));
                }
            }

            AabbMin = min;
            AabbMax = max;

            TriangleMesh = new TriangleMesh(triangles, ignoreDegenerated: true);
        }

        public SubMeshResource GetMesh()
        {
            var pts = Points;
            if (pts == null || pts.Count == 0) return null;

            // Each 3 points = one triangle, flat array: pos(3) normal(3) uv(2)
            var verts = new float[pts.Count * 8];
            for (int i = 0; i < pts.Count; i++)
            {
                int b = i * 8;
                verts[b] = pts[i].X;
                verts[b + 1] = pts[i].Y;
                verts[b + 2] = pts[i].Z;
                verts[b + 3] = 0f; // normal placeholder
                verts[b + 4] = 1f;
                verts[b + 5] = 0f;
                verts[b + 6] = 0f; // uv placeholder
                verts[b + 7] = 0f;
            }

            return new SubMeshResource(verts, 0);
        }

        public void Dispose() { }
    }
}