using System.Numerics;

namespace OssianForge.Engine.Utils
{
    public static class Raycast
    {
        public struct Ray
        {
            public Vector3 Origin;
            public Vector3 Direction; // normalized
        }

        /// <summary>
        /// Unprojects the mouse position into a world-space ray from the current camera.
        /// </summary>
        public static Ray ScreenToRay(Vector2 mousePixels)
        {
            var screen = Engine.Graphics.WindowSize;
            var cam = Engine.Graphics.GetCurrentCamera();

            // NDC: [-1, 1] with Y flipped (screen Y grows down, NDC Y grows up)
            float ndcX = (2f * mousePixels.X / screen.X) - 1f;
            float ndcY = -((2f * mousePixels.Y / screen.Y) - 1f);

            var proj = cam.GetProjection();
            var view = cam.GetView();

            // Unproject two points at different depths to form a direction
            Matrix4x4.Invert(proj, out var invProj);
            Matrix4x4.Invert(view, out var invView);

            // View-space near and far points
            var nearView = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), invProj);
            var farView = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invProj);

            nearView /= nearView.W;
            farView /= farView.W;

            // World-space
            var nearWorld = Vector4.Transform(nearView, invView);
            var farWorld = Vector4.Transform(farView, invView);

            var origin = new Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z);
            var direction = Vector3.Normalize(
                new Vector3(farWorld.X, farWorld.Y, farWorld.Z) - origin);

            return new Ray { Origin = origin, Direction = direction };
        }

        /// <summary>
        /// Möller–Trumbore ray-AABB slab test.
        /// Returns true if the ray hits the box at a positive distance.
        /// </summary>
        public static bool RayIntersectsAABB(Ray ray, Vector3 min, Vector3 max)
        {
            float tMin = float.NegativeInfinity;
            float tMax = float.PositiveInfinity;

            for (int i = 0; i < 3; i++)
            {
                float origin = i == 0 ? ray.Origin.X : i == 1 ? ray.Origin.Y : ray.Origin.Z;
                float dir = i == 0 ? ray.Direction.X : i == 1 ? ray.Direction.Y : ray.Direction.Z;
                float bMin = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
                float bMax = i == 0 ? max.X : i == 1 ? max.Y : max.Z;

                if (MathF.Abs(dir) < 1e-8f)
                {
                    // Ray is parallel to slab — miss if origin outside slab
                    if (origin < bMin || origin > bMax) return false;
                    continue;
                }

                float t1 = (bMin - origin) / dir;
                float t2 = (bMax - origin) / dir;
                if (t1 > t2) (t1, t2) = (t2, t1);

                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMax < 0f || tMin > tMax) return false;
            }

            return true;
        }
    }
}