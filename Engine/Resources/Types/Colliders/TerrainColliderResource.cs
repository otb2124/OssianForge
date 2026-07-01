using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Colliders
{
    public class TerrainColliderResource : ColliderResource
    {
        private readonly string _heightmapMeshId;
        private readonly int _step;

        private List<JTriangle> _triangles = new();

        /// <param name="id">Collider resource ID.</param>
        /// <param name="heightmapMeshId">ID of the HeightmapMeshResource to sample from.</param>
        /// <param name="step">
        /// Grid cell step size. 1 = full resolution (every cell), 2 = half resolution, 4 = quarter, etc.
        /// Higher values are faster but less accurate. 4 is a good default for large terrains.
        /// </param>
        public TerrainColliderResource(string id, string heightmapMeshId, int step = 3)
            : base(id)
        {
            _heightmapMeshId = heightmapMeshId;
            _step = Math.Max(1, step);
        }

        public override void Load()
        {
            base.Load();
            _triangles.Clear();

            var mesh = Engine.Resources.GetResource<HeightmapMeshResource>(_heightmapMeshId)
                ?? throw new Exception($"TerrainColliderResource: HeightmapMeshResource not found: '{_heightmapMeshId}'");

            if (mesh.HeightGrid == null)
                throw new Exception($"TerrainColliderResource: HeightmapMeshResource '{_heightmapMeshId}' has no HeightGrid — was it loaded?");

            int cols = mesh.GridResX; // GridResX/Z already include the +1
            int rows = mesh.GridResZ;

            var min = new JVector(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new JVector(float.MinValue, float.MinValue, float.MinValue);

            // Sample the height grid at _step intervals, building two triangles per quad cell.
            // GetHeightAt handles bilinear interpolation so stepping is safe.
            for (int gz = 0; gz < rows - _step; gz += _step)
            {
                for (int gx = 0; gx < cols - _step; gx += _step)
                {
                    // World XZ positions — mirror HeightmapMeshResource.BuildVertices
                    // which uses ((float)x / _resX - 0.5f) * _width pattern.
                    // We reconstruct by sampling GetHeightAt directly, which already
                    // encodes the XZ → world mapping internally.
                    float u0 = (float)gx / (cols - 1);
                    float u1 = (float)Math.Min(gx + _step, cols - 1) / (cols - 1);
                    float v0 = (float)gz / (rows - 1);
                    float v1 = (float)Math.Min(gz + _step, rows - 1) / (rows - 1);

                    // Reconstruct world XZ from UV — matches BuildVertices formula:
                    // wx = (u - 0.5) * width,  wz = (v - 0.5) * depth
                    float width = cols - 1;
                    float depth = rows - 1;

                    float wx0 = (u0 - 0.5f) * width;
                    float wx1 = (u1 - 0.5f) * width;
                    float wz0 = (v0 - 0.5f) * depth;
                    float wz1 = (v1 - 0.5f) * depth;

                    float h00 = mesh.GetHeightAt(wx0, wz0);
                    float h10 = mesh.GetHeightAt(wx1, wz0);
                    float h01 = mesh.GetHeightAt(wx0, wz1);
                    float h11 = mesh.GetHeightAt(wx1, wz1);

                    var p00 = new JVector(wx0, h00, wz0);
                    var p10 = new JVector(wx1, h10, wz0);
                    var p01 = new JVector(wx0, h01, wz1);
                    var p11 = new JVector(wx1, h11, wz1);

                    // Two triangles per quad, consistent winding with the visual mesh
                    _triangles.Add(new JTriangle(p00, p01, p10));
                    _triangles.Add(new JTriangle(p10, p01, p11));

                    min = JVector.Min(min, JVector.Min(p00, JVector.Min(p10, JVector.Min(p01, p11))));
                    max = JVector.Max(max, JVector.Max(p00, JVector.Max(p10, JVector.Max(p01, p11))));
                }
            }

            AabbMin = min;
            AabbMax = max;

            Console.WriteLine($"[HEIGHTMAP COLLIDER] '{Id}': {_triangles.Count} triangles at step={_step}");
        }

        public override RigidBodyShape CreateDynamicShape(Vector3 nodeScale)
        {
            // Terrain is always static — dynamic use is not supported.
            throw new NotSupportedException("TerrainColliderResource is static-only. Use CreateStaticShapes.");
        }

        public override IEnumerable<RigidBodyShape> CreateStaticShapes(Transform worldTransform)
        {
            if (_triangles.Count == 0)
                yield break;

            // Apply world transform to each triangle vertex.
            // For terrain this is usually identity or a simple translation,
            // but we support full TRS for correctness.
            var transformed = _triangles.Select(tri => new JTriangle(
                TransformVertex(tri.V0, worldTransform),
                TransformVertex(tri.V1, worldTransform),
                TransformVertex(tri.V2, worldTransform)));

            var triMesh = new TriangleMesh(transformed.ToList(), ignoreDegenerated: true);
            foreach (var shape in TriangleShape.CreateAllShapes(triMesh))
                yield return shape;
        }

        public override SubMeshResource GetDebugMesh()
        {
            if (_triangles.Count == 0) return null;

            var verts = new float[_triangles.Count * 3 * 8];
            int cursor = 0;

            foreach (var tri in _triangles)
            {
                // Compute face normal for debug shading
                var edge1 = tri.V1 - tri.V0;
                var edge2 = tri.V2 - tri.V0;
                var normal = JVector.Cross(edge1, edge2);
                float len = normal.Length();
                if (len > 1e-6f) normal = normal * (1f / len);

                WriteVert(verts, ref cursor, tri.V0, normal);
                WriteVert(verts, ref cursor, tri.V1, normal);
                WriteVert(verts, ref cursor, tri.V2, normal);
            }

            return new SubMeshResource(verts, 0);
        }

        private static void WriteVert(float[] buf, ref int cursor, JVector p, JVector n)
        {
            buf[cursor++] = p.X;
            buf[cursor++] = p.Y;
            buf[cursor++] = p.Z;
            buf[cursor++] = n.X;
            buf[cursor++] = n.Y;
            buf[cursor++] = n.Z;
            buf[cursor++] = 0f;
            buf[cursor++] = 0f;
        }

        private static JVector TransformVertex(JVector v, Transform t)
        {
            var matrix = t.ToMatrix();
            var result = Vector3.Transform(new Vector3(v.X, v.Y, v.Z), matrix);
            return new JVector(result.X, result.Y, result.Z);
        }
    }
}