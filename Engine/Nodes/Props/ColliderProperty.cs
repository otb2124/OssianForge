using OssianForge.Engine.Resources.Colliders;
using System;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{


    public class ColliderProperty : NodeProperty
    {

        public bool IsTrigger;
        public Action<Node> OnCollision;
        public ColliderResource ColliderResource;

        public ColliderProperty(string colliderId, bool isTrigger = false)
        {
            ColliderResource = Engine.Resources.GetResource(colliderId) as ColliderResource;
            IsTrigger = isTrigger;
        }

        // ----------------------------------------------------------------
        // Broad + narrow phase Intersects
        // ----------------------------------------------------------------
        public bool Intersects(ColliderProperty other,
                                        TransformProperty selfT, TransformProperty otherT)
        {
            // Mesh vs Mesh
            if (other is ColliderProperty otherMesh)
            {
                foreach (var subA in ColliderResource.SubColliders)
                    foreach (var subB in otherMesh.ColliderResource.SubColliders)
                    {
                        if (!subA.AABBIntersects(subB, selfT, otherT)) continue;
                        if (NarrowPhase(subA, subB, selfT, otherT)) return true;
                    }
                return false;
            }

            return false;
        }

        // ----------------------------------------------------------------
        // ResolveOverlap — returns the push vector for selfT
        // ----------------------------------------------------------------
        public Vector3 ResolveOverlap(ColliderProperty other,
               TransformProperty selfT, TransformProperty otherT)
        {
            const float minPush = 0.001f;

            var (aMin, aMax) = GetWorldAABB(selfT);
            var (bMin, bMax) = other.GetWorldAABB(otherT);
            float overlapX = MathF.Min(aMax.X, bMax.X) - MathF.Max(aMin.X, bMin.X);
            float overlapY = MathF.Min(aMax.Y, bMax.Y) - MathF.Max(aMin.Y, bMin.Y);
            float overlapZ = MathF.Min(aMax.Z, bMax.Z) - MathF.Max(aMin.Z, bMin.Z);

            if (overlapX <= 0 || overlapY <= 0 || overlapZ <= 0)
                return Vector3.Zero;

            if (overlapY <= overlapX && overlapY <= overlapZ)
            {
                if (overlapY < minPush) return Vector3.Zero;
                float centerAY = (aMin.Y + aMax.Y) * 0.5f;
                float centerBY = (bMin.Y + bMax.Y) * 0.5f;
                float dir = centerBY >= centerAY ? 1f : -1f;
                return new Vector3(0, dir * overlapY, 0);
            }
            else if (overlapX <= overlapY && overlapX <= overlapZ)
            {
                if (overlapX < minPush) return Vector3.Zero;
                float centerAX = (aMin.X + aMax.X) * 0.5f;
                float centerBX = (bMin.X + bMax.X) * 0.5f;
                float dir = centerBX >= centerAX ? 1f : -1f;
                return new Vector3(dir * overlapX, 0, 0);
            }
            else
            {
                if (overlapZ < minPush) return Vector3.Zero;
                float centerAZ = (aMin.Z + aMax.Z) * 0.5f;
                float centerBZ = (bMin.Z + bMax.Z) * 0.5f;
                float dir = centerBZ >= centerAZ ? 1f : -1f;
                return new Vector3(0, 0, dir * overlapZ);
            }
        }

        private (Vector3 Min, Vector3 Max) GetWorldAABB(TransformProperty t, float skin = 0f)
        {
            Vector3 worldMin = new Vector3(float.MaxValue);
            Vector3 worldMax = new Vector3(float.MinValue);

            foreach (var sub in ColliderResource.SubColliders)
                foreach (var tri in sub.Triangles)
                    foreach (var v in new[] { tri.A, tri.B, tri.C })
                    {
                        var world = TransformPoint(v, t);
                        worldMin = Vector3.Min(worldMin, world);
                        worldMax = Vector3.Max(worldMax, world);
                    }

            return (worldMin - new Vector3(skin), worldMax + new Vector3(skin));
        }


        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static Vector3 TransformPoint(Vector3 local, TransformProperty t)
        {
            var matrix = t.Transform.ToMatrix();
            return Vector3.Transform(local, matrix);
        }

        private static Vector3 TransformNormal(Vector3 normal, TransformProperty t)
        {
            var matrix = t.Transform.ToMatrix();
            Matrix4x4.Invert(matrix, out var inv);
            return Vector3.Normalize(Vector3.TransformNormal(normal, Matrix4x4.Transpose(inv)));
        }

        private static bool NarrowPhase(SubCollider a, SubCollider b,
                                TransformProperty tA, TransformProperty tB)
        {
            var (bMin, bMax) = b.WorldAABB(tB);
            float skin = 0.01f;
            bMin -= new Vector3(skin);
            bMax += new Vector3(skin);

            foreach (var tri in a.Triangles)
            {
                var wa = TransformPoint(tri.A, tA);
                var wb = TransformPoint(tri.B, tA);
                var wc = TransformPoint(tri.C, tA);
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

        private static float PointTriangleDist(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            // Closest point on triangle to p
            var ab = b - a; var ac = c - a; var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return Vector3.Distance(p, a);

            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return Vector3.Distance(p, b);

            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return Vector3.Distance(p, c);

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                float v = d1 / (d1 - d3);
                return Vector3.Distance(p, a + v * ab);
            }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                float w = d2 / (d2 - d6);
                return Vector3.Distance(p, a + w * ac);
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return Vector3.Distance(p, b + w * (c - b));
            }

            float denom = 1f / (va + vb + vc);
            var closest = a + ab * (vb * denom) + ac * (vc * denom);
            return Vector3.Distance(p, closest);
        }

        private static float EstimatePenetrationDepth(SubCollider.Triangle tri,
                                                      SubCollider other,
                                                      TransformProperty selfT,
                                                      TransformProperty otherT)
        {
            // Project AABB of other onto triangle normal, compare with triangle plane
            var (oMin, oMax) = other.WorldAABB(otherT);
            var oCenter = (oMin + oMax) * 0.5f;
            var wa = TransformPoint(tri.A, selfT);
            var wn = TransformNormal(tri.Normal, selfT);

            float planeDist = Vector3.Dot(wn, oCenter) - Vector3.Dot(wn, wa);
            return MathF.Max(0f, -planeDist + 0.05f); // small bias
        }
    }
}