using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Colliders
{
    public class MeshColliderResource : ColliderResource
    {
        private List<JVector> _points = new();
        private List<JTriangle> _triangles = new();
        public string MeshResourceId { get; set; }


        public MeshColliderResource(string id, string meshResourceId)
            : base(id) 
        {
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            base.Load();
            _points.Clear();
            _triangles.Clear();

            var source = Engine.Resources.GetResource<MeshResource>(MeshResourceId);
            var min = new JVector(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new JVector(float.MinValue, float.MinValue, float.MinValue);

            foreach (var sub in source.SubMeshes)
            {
                const int stride = 8;
                var v = sub.RawVertices;

                JVector ReadVertex(int idx) => new JVector(
                    v[idx] - source.HipsOffset.X,
                    v[idx + 1] - source.HipsOffset.Y,
                    v[idx + 2] - source.HipsOffset.Z);

                for (int i = 0; i + stride * 3 <= v.Length; i += stride * 3)
                {
                    var a = ReadVertex(i);
                    var b = ReadVertex(i + stride);
                    var c = ReadVertex(i + stride * 2);

                    _triangles.Add(new JTriangle(a, b, c));
                    _points.Add(a);
                    _points.Add(b);
                    _points.Add(c);

                    min = JVector.Min(min, JVector.Min(a, JVector.Min(b, c)));
                    max = JVector.Max(max, JVector.Max(a, JVector.Max(b, c)));
                }
            }

            AabbMin = min;
            AabbMax = max;
        }

        public override RigidBodyShape CreateDynamicShape(Vector3 nodeScale)
        {
            var centroid = new JVector(
                (AabbMin.X + AabbMax.X) * 0.5f * nodeScale.X,
                (AabbMin.Y + AabbMax.Y) * 0.5f * nodeScale.Y,
                (AabbMin.Z + AabbMax.Z) * 0.5f * nodeScale.Z);

            var centeredPoints = _points
                .Select(p => new JVector(
                    p.X * nodeScale.X - centroid.X,
                    p.Y * nodeScale.Y - centroid.Y,
                    p.Z * nodeScale.Z - centroid.Z))
                .ToList();

            return new PointCloudShape(centeredPoints);
        }

        public override IEnumerable<RigidBodyShape> CreateStaticShapes(Transform worldTransform)
        {
            var transformed = _triangles.Select(tri => new JTriangle(
                TransformVertex(tri.V0, worldTransform),
                TransformVertex(tri.V1, worldTransform),
                TransformVertex(tri.V2, worldTransform)));

            var mesh = new TriangleMesh(transformed.ToList(), ignoreDegenerated: true);
            return TriangleShape.CreateAllShapes(mesh);
        }

        public override SubMeshResource GetDebugMesh()
        {
            if (_points.Count == 0) return null;

            var verts = new float[_points.Count * 8];
            for (int i = 0; i < _points.Count; i++)
            {
                int b = i * 8;
                verts[b] = _points[i].X;
                verts[b + 1] = _points[i].Y;
                verts[b + 2] = _points[i].Z;
                verts[b + 3] = 0f;
                verts[b + 4] = 1f;
                verts[b + 5] = 0f;
                verts[b + 6] = 0f;
                verts[b + 7] = 0f;
            }
            return new SubMeshResource(verts, 0);
        }

        private static JVector TransformVertex(JVector v, Transform t)
        {
            var matrix = t.ToMatrix();
            var result = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), matrix);
            return new JVector(result.X, result.Y, result.Z);
        }
    }
}