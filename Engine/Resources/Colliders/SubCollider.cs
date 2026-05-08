using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Resources.Colliders
{
    /// <summary>
    /// Pre-baked triangle soup for one submesh. Built once at Load() time.
    /// </summary>
    public class SubCollider
    {
        public struct Triangle
        {
            public Vector3 A, B, C;
            public Vector3 Normal;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a; B = b; C = c;
                var edge1 = b - a;
                var edge2 = c - a;
                var cross = Vector3.Cross(edge1, edge2);
                Normal = cross.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(cross)
                    : Vector3.UnitY;
            }
        }

        public List<Triangle> Triangles = new();

        // Cached AABB in local space for broad-phase rejection
        public Vector3 LocalMin;
        public Vector3 LocalMax;

        /// <summary>
        /// Parse raw vertex float array into triangles.
        /// Stride: pos(3) + optional normals(3) + optional UV(2)
        /// </summary>
        public void Bake(float[] vertices, bool hasNormals, bool hasUV)
        {
            int stride = 3;
            if (hasNormals) stride += 3;
            if (hasUV) stride += 2;

            LocalMin = new Vector3(float.MaxValue);
            LocalMax = new Vector3(float.MinValue);

            for (int i = 0; i + stride * 3 <= vertices.Length; i += stride * 3)
            {
                var a = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
                var b = new Vector3(vertices[i + stride], vertices[i + stride + 1], vertices[i + stride + 2]);
                var c = new Vector3(vertices[i + stride * 2], vertices[i + stride * 2 + 1], vertices[i + stride * 2 + 2]);

                Triangles.Add(new Triangle(a, b, c));

                LocalMin = Vector3.Min(LocalMin, Vector3.Min(a, Vector3.Min(b, c)));
                LocalMax = Vector3.Max(LocalMax, Vector3.Max(a, Vector3.Max(b, c)));
            }
        }

        // World-space AABB after applying transform scale+position
        public (Vector3 min, Vector3 max) WorldAABB(TransformProperty t)
        {
            var center = t.Transform.Position;
            var scaled = (LocalMax - LocalMin) * t.Transform.Scale * 0.5f;
            var mid = (LocalMin + LocalMax) * 0.5f * t.Transform.Scale + center;
            return (mid - scaled, mid + scaled);
        }

        public bool AABBIntersects(SubCollider other,
                                   TransformProperty selfT, TransformProperty otherT)
        {
            var (aMin, aMax) = WorldAABB(selfT);
            var (bMin, bMax) = other.WorldAABB(otherT);
            return aMin.X <= bMax.X && aMax.X >= bMin.X &&
                   aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                   aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;
        }
    }
}