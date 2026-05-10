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
                var cross = Vector3.Cross(b - a, c - a);
                Normal = cross.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(cross) : Vector3.UnitY;
            }
        }

        public List<Triangle> Triangles = new();
        public Vector3 LocalMin;
        public Vector3 LocalMax;

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

        public (Vector3 min, Vector3 max) WorldAABB(TransformProperty t)
        {
            var matrix = t.Transform.ToMatrix();
            Span<Vector3> corners = stackalloc Vector3[8];
            corners[0] = Vector3.Transform(new Vector3(LocalMin.X, LocalMin.Y, LocalMin.Z), matrix);
            corners[1] = Vector3.Transform(new Vector3(LocalMax.X, LocalMin.Y, LocalMin.Z), matrix);
            corners[2] = Vector3.Transform(new Vector3(LocalMin.X, LocalMax.Y, LocalMin.Z), matrix);
            corners[3] = Vector3.Transform(new Vector3(LocalMax.X, LocalMax.Y, LocalMin.Z), matrix);
            corners[4] = Vector3.Transform(new Vector3(LocalMin.X, LocalMin.Y, LocalMax.Z), matrix);
            corners[5] = Vector3.Transform(new Vector3(LocalMax.X, LocalMin.Y, LocalMax.Z), matrix);
            corners[6] = Vector3.Transform(new Vector3(LocalMin.X, LocalMax.Y, LocalMax.Z), matrix);
            corners[7] = Vector3.Transform(new Vector3(LocalMax.X, LocalMax.Y, LocalMax.Z), matrix);

            var wMin = corners[0]; var wMax = corners[0];
            for (int i = 1; i < 8; i++)
            {
                wMin = Vector3.Min(wMin, corners[i]);
                wMax = Vector3.Max(wMax, corners[i]);
            }
            return (wMin, wMax);
        }

        public bool AABBIntersects(SubCollider other, TransformProperty selfT, TransformProperty otherT)
        {
            var (aMin, aMax) = WorldAABB(selfT);
            var (bMin, bMax) = other.WorldAABB(otherT);
            const float skin = 0.01f;
            aMin -= new Vector3(skin);
            aMax += new Vector3(skin);
            return aMin.X <= bMax.X && aMax.X >= bMin.X &&
                   aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                   aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;
        }

        /// <summary>Merged AABB across all submeshes — used by ColliderProperty.</summary>
        public static (Vector3 min, Vector3 max) MergedWorldAABB(
            IEnumerable<SubCollider> subs, TransformProperty t)
        {
            var wMin = new Vector3(float.MaxValue);
            var wMax = new Vector3(float.MinValue);
            foreach (var sub in subs)
            {
                var (sMin, sMax) = sub.WorldAABB(t);
                wMin = Vector3.Min(wMin, sMin);
                wMax = Vector3.Max(wMax, sMax);
            }
            return (wMin, wMax);
        }

        public bool NarrowPhase(SubCollider other, TransformProperty selfT, TransformProperty otherT)
        {
            var (bMin, bMax) = other.WorldAABB(otherT);
            const float skin = 0.01f;
            bMin -= new Vector3(skin);
            bMax += new Vector3(skin);

            var matrix = selfT.Transform.ToMatrix();
            foreach (var tri in Triangles)
            {
                var wa = Vector3.Transform(tri.A, matrix);
                var wb = Vector3.Transform(tri.B, matrix);
                var wc = Vector3.Transform(tri.C, matrix);
                if (TriangleAABB(wa, wb, wc, bMin, bMax)) return true;
            }
            return false;
        }

        private static bool TriangleAABB(Vector3 a, Vector3 b, Vector3 c,
                                         Vector3 boxMin, Vector3 boxMax)
        {
            var triMin = Vector3.Min(a, Vector3.Min(b, c));
            var triMax = Vector3.Max(a, Vector3.Max(b, c));
            return triMin.X <= boxMax.X && triMax.X >= boxMin.X &&
                   triMin.Y <= boxMax.Y && triMax.Y >= boxMin.Y &&
                   triMin.Z <= boxMax.Z && triMax.Z >= boxMin.Z;
        }
    }
}
