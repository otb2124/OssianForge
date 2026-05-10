using OssianForge.Engine.Resources.Colliders;
using System;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{


    public class ColliderProperty : NodeProperty
    {
        public bool IsTrigger;
        public ColliderResource ColliderResource;

        public ColliderProperty(string colliderId, bool isTrigger = false)
        {
            ColliderResource = Engine.Resources.GetResource(colliderId) as ColliderResource;
            IsTrigger = isTrigger;
        }

        public bool Intersects(ColliderProperty other,
                               TransformProperty selfT, TransformProperty otherT)
        {
            foreach (var subA in ColliderResource.SubColliders)
                foreach (var subB in other.ColliderResource.SubColliders)
                {
                    if (!subA.AABBIntersects(subB, selfT, otherT)) continue;
                    if (subA.NarrowPhase(subB, selfT, otherT)) return true;
                }
            return false;
        }

        public Vector3 ResolveOverlap(ColliderProperty other,
                                      TransformProperty selfT, TransformProperty otherT)
        {
            const float minPush = 0.001f;

            var (aMin, aMax) = SubCollider.MergedWorldAABB(ColliderResource.SubColliders, selfT);
            var (bMin, bMax) = SubCollider.MergedWorldAABB(other.ColliderResource.SubColliders, otherT);

            float overlapX = MathF.Min(aMax.X, bMax.X) - MathF.Max(aMin.X, bMin.X);
            float overlapY = MathF.Min(aMax.Y, bMax.Y) - MathF.Max(aMin.Y, bMin.Y);
            float overlapZ = MathF.Min(aMax.Z, bMax.Z) - MathF.Max(aMin.Z, bMin.Z);

            if (overlapX <= 0 || overlapY <= 0 || overlapZ <= 0)
                return Vector3.Zero;

            if (overlapY <= overlapX && overlapY <= overlapZ)
            {
                if (overlapY < minPush) return Vector3.Zero;
                float dir = (bMin.Y + bMax.Y) >= (aMin.Y + aMax.Y) ? 1f : -1f;
                return new Vector3(0, dir * overlapY, 0);
            }
            else if (overlapX <= overlapY && overlapX <= overlapZ)
            {
                if (overlapX < minPush) return Vector3.Zero;
                float dir = (bMin.X + bMax.X) >= (aMin.X + aMax.X) ? 1f : -1f;
                return new Vector3(dir * overlapX, 0, 0);
            }
            else
            {
                if (overlapZ < minPush) return Vector3.Zero;
                float dir = (bMin.Z + bMax.Z) >= (aMin.Z + aMax.Z) ? 1f : -1f;
                return new Vector3(0, 0, dir * overlapZ);
            }
        }
    }
}