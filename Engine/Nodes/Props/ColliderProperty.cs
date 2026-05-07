using System.Numerics;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public abstract class ColliderProperty : NodeProperty
    {
        public bool IsTrigger;
        public Action<Node> OnCollision;

        public abstract bool Intersects(ColliderProperty other, TransformProperty selfTransform, TransformProperty otherTransform);
        public abstract Vector3 ResolveOverlap(ColliderProperty other, TransformProperty selfTransform, TransformProperty otherTransform);
    }

    public class BoxColliderProperty : ColliderProperty
    {
        public Vector3 Size; // half-extents
        public Vector3 Offset;

        public BoxColliderProperty(Vector3 size, Vector3 offset = default, bool isTrigger = false)
        {
            Size = size;
            Offset = offset;
            IsTrigger = isTrigger;
        }

        private Vector3 WorldCenter(TransformProperty t) =>
            t.Transform.Position + Offset;

        private Vector3 WorldHalfExtents(TransformProperty t) =>
            Size * t.Transform.Scale * 0.5f;

        public override bool Intersects(ColliderProperty other, TransformProperty selfT, TransformProperty otherT)
        {
            if (other is BoxColliderProperty box)
            {
                var aMin = WorldCenter(selfT) - WorldHalfExtents(selfT);
                var aMax = WorldCenter(selfT) + WorldHalfExtents(selfT);
                var bMin = WorldCenter(otherT) - box.WorldHalfExtents(otherT);
                var bMax = WorldCenter(otherT) + box.WorldHalfExtents(otherT);

                return aMin.X <= bMax.X && aMax.X >= bMin.X &&
                       aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                       aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;
            }
            if (other is SphereColliderProperty sphere)
                return sphere.Intersects(this, otherT, selfT);

            return false;
        }

        public override Vector3 ResolveOverlap(ColliderProperty other, TransformProperty selfT, TransformProperty otherT)
        {
            if (other is not BoxColliderProperty box) return Vector3.Zero;

            var aCenter = WorldCenter(selfT);
            var bCenter = WorldCenter(otherT);
            var aHalf = WorldHalfExtents(selfT);
            var bHalf = box.WorldHalfExtents(otherT);

            var diff = aCenter - bCenter;
            var overlap = (aHalf + bHalf) - new Vector3(MathF.Abs(diff.X), MathF.Abs(diff.Y), MathF.Abs(diff.Z));

            // Push out along axis of least overlap
            if (overlap.X < overlap.Y && overlap.X < overlap.Z)
                return new Vector3(MathF.Sign(diff.X) * overlap.X, 0, 0);
            if (overlap.Y < overlap.X && overlap.Y < overlap.Z)
                return new Vector3(0, MathF.Sign(diff.Y) * overlap.Y, 0);
            return new Vector3(0, 0, MathF.Sign(diff.Z) * overlap.Z);
        }
    }

    public class SphereColliderProperty : ColliderProperty
    {
        public float Radius;
        public Vector3 Offset;

        public SphereColliderProperty(float radius, Vector3 offset = default, bool isTrigger = false)
        {
            Radius = radius;
            Offset = offset;
            IsTrigger = isTrigger;
        }

        private Vector3 WorldCenter(TransformProperty t) =>
            t.Transform.Position + Offset;

        private float WorldRadius(TransformProperty t) =>
            Radius * MathF.Max(t.Transform.Scale.X, MathF.Max(t.Transform.Scale.Y, t.Transform.Scale.Z));

        public override bool Intersects(ColliderProperty other, TransformProperty selfT, TransformProperty otherT)
        {
            if (other is SphereColliderProperty sphere)
            {
                float dist = Vector3.Distance(WorldCenter(selfT), sphere.WorldCenter(otherT));
                return dist < WorldRadius(selfT) + sphere.WorldRadius(otherT);
            }
            if (other is BoxColliderProperty box)
            {
                // Closest point on box to sphere center
                var center = WorldCenter(selfT);
                var boxMin = otherT.Transform.Position + box.Offset - box.Size * otherT.Transform.Scale * 0.5f;
                var boxMax = otherT.Transform.Position + box.Offset + box.Size * otherT.Transform.Scale * 0.5f;
                var closest = Vector3.Clamp(center, boxMin, boxMax);
                return Vector3.Distance(center, closest) < WorldRadius(selfT);
            }
            return false;
        }

        public override Vector3 ResolveOverlap(ColliderProperty other, TransformProperty selfT, TransformProperty otherT)
        {
            if (other is not SphereColliderProperty sphere) return Vector3.Zero;

            var diff = WorldCenter(selfT) - sphere.WorldCenter(otherT);
            float dist = diff.Length();
            float overlap = WorldRadius(selfT) + sphere.WorldRadius(otherT) - dist;
            if (overlap <= 0) return Vector3.Zero;
            return Vector3.Normalize(diff) * overlap;
        }
    }
}