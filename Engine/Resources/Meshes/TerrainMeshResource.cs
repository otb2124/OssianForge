using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.TextureFiles;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.IO;
using System.Numerics;
using OssianForge.Engine.Resources;
using OssianForge.Engine;

namespace OssianForge.Engine.Resources.Meshes
{
    public class TerrainMeshResource : MeshResource
    {
        private readonly string _heightmapId;
        private float _maxHeight;

        private float _width;
        private float _depth;
        private int _resX;
        private int _resZ;

        public float[] HeightGrid { get; private set; }
        public int GridResX => _resX + 1;
        public int GridResZ => _resZ + 1;

        public TerrainMeshResource(
            string id,
            string heightmapFileId,
            float maxHeight = -1f)
            : base(id, "fastmesh.quad")
        {
            _heightmapId = heightmapFileId;
            _maxHeight = maxHeight;
        }

        public override void Load()
        {
            var texFile = Engine.Resources.GetResourceFile<TextureFile>(_heightmapId)
                ?? throw new Exception($"TerrainMeshResource: heightmap TextureFile not found: '{_heightmapId}'");

            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + texFile.Path;

            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult image;
            using (var stream = File.OpenRead(globalPath))
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            _width = image.Width;
            _depth = image.Height;
            _resX = image.Width;
            _resZ = image.Height;

            if (_maxHeight < 0f)
                _maxHeight = Math.Min(_width, _depth) * 0.15f;

            float[] heights = SampleHeightmap(image);
            float[] vertices = BuildVertices(heights);

            SubMeshes.Clear();
            SubMeshes.Add(new SubMeshResource(vertices, materialIndex: 0, bones: null));

            RebuildAabb();
        }

        private float[] SampleHeightmap(ImageResult image)
        {
            int imgW = image.Width;
            int imgH = image.Height;
            int cols = _resX + 1;
            int rows = _resZ + 1;

            HeightGrid = new float[cols * rows];

            for (int z = 0; z < rows; z++)
            {
                int py = Math.Min(z, imgH - 1);
                for (int x = 0; x < cols; x++)
                {
                    int px = Math.Min(x, imgW - 1);
                    HeightGrid[z * cols + x] = Luminance(image, px, py, imgW);
                }
            }

            return HeightGrid;
        }

        private static float Luminance(ImageResult img, int px, int py, int imgW)
        {
            int idx = (py * imgW + px) * 4;
            float r = img.Data[idx] / 255f;
            float g = img.Data[idx + 1] / 255f;
            float b = img.Data[idx + 2] / 255f;
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        private float[] BuildVertices(float[] heights)
        {
            int cols = _resX + 1;
            int rows = _resZ + 1;

            var positions = new Vector3[rows * cols];
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float wx = ((float)x / _resX - 0.5f) * _width;
                    float wy = heights[z * cols + x] * _maxHeight;
                    float wz = ((float)z / _resZ - 0.5f) * _depth;
                    positions[z * cols + x] = new Vector3(wx, wy, wz);
                }
            }

            int triCount = _resX * _resZ * 2;
            var verts = new float[triCount * 3 * 8];
            int cursor = 0;

            for (int qz = 0; qz < _resZ; qz++)
            {
                for (int qx = 0; qx < _resX; qx++)
                {
                    int i00 = qz * cols + qx;
                    int i10 = qz * cols + qx + 1;
                    int i01 = (qz + 1) * cols + qx;
                    int i11 = (qz + 1) * cols + qx + 1;

                    Vector3 p00 = positions[i00];
                    Vector3 p10 = positions[i10];
                    Vector3 p01 = positions[i01];
                    Vector3 p11 = positions[i11];

                    float u0 = (float)qx / _resX;
                    float u1 = (float)(qx + 1) / _resX;
                    float v0 = (float)qz / _resZ;
                    float v1 = (float)(qz + 1) / _resZ;

                    Vector3 n00 = SmoothNormal(positions, cols, rows, qx, qz);
                    Vector3 n10 = SmoothNormal(positions, cols, rows, qx + 1, qz);
                    Vector3 n01 = SmoothNormal(positions, cols, rows, qx, qz + 1);
                    Vector3 n11 = SmoothNormal(positions, cols, rows, qx + 1, qz + 1);

                    WriteVertex(verts, ref cursor, p00, n00, u0, v0);
                    WriteVertex(verts, ref cursor, p10, n10, u1, v0);
                    WriteVertex(verts, ref cursor, p01, n01, u0, v1);

                    WriteVertex(verts, ref cursor, p10, n10, u1, v0);
                    WriteVertex(verts, ref cursor, p11, n11, u1, v1);
                    WriteVertex(verts, ref cursor, p01, n01, u0, v1);
                }
            }

            return verts;
        }

        private static Vector3 SmoothNormal(Vector3[] pos, int cols, int rows, int gx, int gz)
        {
            int xL = Math.Max(gx - 1, 0), xR = Math.Min(gx + 1, cols - 1);
            int zD = Math.Max(gz - 1, 0), zU = Math.Min(gz + 1, rows - 1);

            Vector3 left = pos[gz * cols + xL];
            Vector3 right = pos[gz * cols + xR];
            Vector3 down = pos[zD * cols + gx];
            Vector3 up = pos[zU * cols + gx];

            Vector3 tx = right - left;
            Vector3 tz = up - down;

            return Vector3.Normalize(Vector3.Cross(tz, tx));
        }

        private static void WriteVertex(float[] buf, ref int cursor,
                                        Vector3 pos, Vector3 norm, float u, float v)
        {
            buf[cursor++] = pos.X;
            buf[cursor++] = pos.Y;
            buf[cursor++] = pos.Z;
            buf[cursor++] = norm.X;
            buf[cursor++] = norm.Y;
            buf[cursor++] = norm.Z;
            buf[cursor++] = u;
            buf[cursor++] = v;
        }

        private void RebuildAabb()
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var sub in SubMeshes)
            {
                var v = sub.RawVertices;
                const int stride = 8;
                for (int i = 0; i + stride <= v.Length; i += stride)
                {
                    var p = new Vector3(v[i], v[i + 1], v[i + 2]);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            _localAabbMin = min;
            _localAabbMax = max;
        }

        private Vector3 _localAabbMin = new Vector3(float.MaxValue);
        private Vector3 _localAabbMax = new Vector3(float.MinValue);
        public new Vector3 LocalAabbMin => _localAabbMin;
        public new Vector3 LocalAabbMax => _localAabbMax;

        public float GetHeightAt(float x, float z)
        {
            if (HeightGrid == null) return 0f;

            float u = (x / _width) + 0.5f;
            float v = (z / _depth) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;

            float gx = u * _resX;
            float gz = v * _resZ;
            int x0 = (int)gx, z0 = (int)gz;
            int x1 = Math.Min(x0 + 1, _resX);
            int z1 = Math.Min(z0 + 1, _resZ);
            float fx = gx - x0, fz = gz - z0;

            int cols = _resX + 1;
            float h00 = HeightGrid[z0 * cols + x0];
            float h10 = HeightGrid[z0 * cols + x1];
            float h01 = HeightGrid[z1 * cols + x0];
            float h11 = HeightGrid[z1 * cols + x1];

            float h = h00 * (1 - fx) * (1 - fz)
                    + h10 * fx * (1 - fz)
                    + h01 * (1 - fx) * fz
                    + h11 * fx * fz;

            return h * _maxHeight;
        }
    }
}