using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Colliders
{
    public class CapsuleColliderResource : ColliderResource
    {
        public float Radius { get; private set; }
        public float Height { get; private set; }

        public CapsuleColliderResource(string id, float radius, float height)
            : base(id)
        {
            Radius = radius;
            Height = height;
            float halfH = height * 0.5f;
            AabbMin = new JVector(-radius, -halfH, -radius);
            AabbMax = new JVector(radius, halfH, radius);
        }

        public override void Load() { }

        public override RigidBodyShape CreateDynamicShape(Vector3 nodeScale)
        {
            float scaledRadius = Radius * MathF.Max(nodeScale.X, nodeScale.Z);
            float scaledHeight = Height * nodeScale.Y;
            float cylinderLength = MathF.Max(0f, scaledHeight - 2f * scaledRadius);
            return new CapsuleShape(scaledRadius, cylinderLength);
        }

        public override IEnumerable<RigidBodyShape> CreateStaticShapes(Transform worldTransform)
            => Enumerable.Empty<RigidBodyShape>();

        public override SubMeshResource GetDebugMesh()
        {
            float r = Radius;
            float halfH = Height / 2f - r;
            if (halfH < 0f) halfH = 0f;

            const int slices = 16;
            const int stacks = 8;

            var verts = new List<float>();

            (Vector3 pos, Vector3 normal) CapsuleVertex(float phi, float theta, bool top)
            {
                var n = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Cos(phi),
                    MathF.Sin(phi) * MathF.Sin(theta));
                float yOffset = top ? halfH : -halfH;
                return (
                    new Vector3(n.X * r, (top ? n.Y : -n.Y) * r + yOffset, n.Z * r),
                    new Vector3(n.X, top ? n.Y : -n.Y, n.Z));
            }

            void Add(Vector3 p, Vector3 n, float u, float v)
            {
                verts.Add(p.X); verts.Add(p.Y); verts.Add(p.Z);
                verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
                verts.Add(u); verts.Add(v);
            }

            // Top hemisphere
            for (int i = 0; i < stacks / 2; i++)
            {
                float phi0 = MathF.PI * i / stacks;
                float phi1 = MathF.PI * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    float t0 = 2f * MathF.PI * j / slices;
                    float t1 = 2f * MathF.PI * (j + 1) / slices;
                    var (p00, n00) = CapsuleVertex(phi0, t0, true);
                    var (p10, n10) = CapsuleVertex(phi1, t0, true);
                    var (p01, n01) = CapsuleVertex(phi0, t1, true);
                    var (p11, n11) = CapsuleVertex(phi1, t1, true);
                    float u0 = (float)j / slices, v0 = (float)i / stacks;
                    float u1 = (float)(j + 1) / slices, v1 = (float)(i + 1) / stacks;
                    Add(p00, n00, u0, v0); Add(p10, n10, u0, v1); Add(p11, n11, u1, v1);
                    Add(p00, n00, u0, v0); Add(p11, n11, u1, v1); Add(p01, n01, u1, v0);
                }
            }

            // Bottom hemisphere
            for (int i = stacks / 2; i < stacks; i++)
            {
                float phi0 = MathF.PI * i / stacks;
                float phi1 = MathF.PI * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    float t0 = 2f * MathF.PI * j / slices;
                    float t1 = 2f * MathF.PI * (j + 1) / slices;
                    var (p00, n00) = CapsuleVertex(phi0, t0, false);
                    var (p10, n10) = CapsuleVertex(phi1, t0, false);
                    var (p01, n01) = CapsuleVertex(phi0, t1, false);
                    var (p11, n11) = CapsuleVertex(phi1, t1, false);
                    float u0 = (float)j / slices, v0 = (float)i / stacks;
                    float u1 = (float)(j + 1) / slices, v1 = (float)(i + 1) / stacks;
                    Add(p00, n00, u0, v0); Add(p10, n10, u0, v1); Add(p11, n11, u1, v1);
                    Add(p00, n00, u0, v0); Add(p11, n11, u1, v1); Add(p01, n01, u1, v0);
                }
            }

            // Cylinder body
            for (int j = 0; j < slices; j++)
            {
                float t0 = 2f * MathF.PI * j / slices;
                float t1 = 2f * MathF.PI * (j + 1) / slices;
                float x0 = MathF.Cos(t0), z0 = MathF.Sin(t0);
                float x1 = MathF.Cos(t1), z1 = MathF.Sin(t1);
                float u0 = (float)j / slices, u1 = (float)(j + 1) / slices;
                Add(new Vector3(x0 * r, -halfH, z0 * r), new Vector3(x0, 0, z0), u0, 0f);
                Add(new Vector3(x1 * r, -halfH, z1 * r), new Vector3(x1, 0, z1), u1, 0f);
                Add(new Vector3(x1 * r, halfH, z1 * r), new Vector3(x1, 0, z1), u1, 1f);
                Add(new Vector3(x0 * r, -halfH, z0 * r), new Vector3(x0, 0, z0), u0, 0f);
                Add(new Vector3(x1 * r, halfH, z1 * r), new Vector3(x1, 0, z1), u1, 1f);
                Add(new Vector3(x0 * r, halfH, z0 * r), new Vector3(x0, 0, z0), u0, 1f);
            }

            return new SubMeshResource(verts.ToArray(), 0);
        }
    }
}