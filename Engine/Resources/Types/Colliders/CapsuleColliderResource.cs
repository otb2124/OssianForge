using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine;

public class CapsuleColliderResource : ColliderResource
{
    public float Radius { get; private set; }
    public float Height { get; private set; }

    public CapsuleColliderResource(string id, float radius, float height) : base(id, "mesh.capsule")
    {
        Id = id;
        Radius = radius;
        Height = height;
        //
    }

    public override void Load()
    {
        Points.Clear();

        float halfH = Height * 0.5f;
        float r = Radius;

        // Generate capsule triangles for Jitter TriangleMesh + Points
        var triangles = new List<JTriangle>();
        const int slices = 16;
        const int stacks = 8;

        // Hemisphere points helper
        JVector CapVertex(float phi, float theta, bool top)
        {
            float x = MathF.Sin(phi) * MathF.Cos(theta);
            float y = MathF.Cos(phi);
            float z = MathF.Sin(phi) * MathF.Sin(theta);

            float yPos = top
                ? y * r + (Height - r)   // top: equator at Height-r, tip at Height
                : y * r + r;              // bottom: equator at r, tip at 0

            return new JVector(x * r, yPos, z * r);
        }

        void AddTri(JVector a, JVector b, JVector c)
        {
            triangles.Add(new JTriangle(a, b, c));
            Points.Add(a);
            Points.Add(b);
            Points.Add(c);
        }

        // Top hemisphere
        for (int i = 0; i < stacks / 2; i++)
        {
            float phi0 = MathF.PI * i / stacks;
            float phi1 = MathF.PI * (i + 1) / stacks;
            for (int j = 0; j < slices; j++)
            {
                float t0 = 2 * MathF.PI * j / slices;
                float t1 = 2 * MathF.PI * (j + 1) / slices;
                var p00 = CapVertex(phi0, t0, true);
                var p10 = CapVertex(phi1, t0, true);
                var p01 = CapVertex(phi0, t1, true);
                var p11 = CapVertex(phi1, t1, true);
                AddTri(p00, p10, p11);
                AddTri(p00, p11, p01);
            }
        }

        // Bottom hemisphere
        for (int i = stacks / 2; i < stacks; i++)
        {
            float phi0 = MathF.PI * i / stacks;
            float phi1 = MathF.PI * (i + 1) / stacks;
            for (int j = 0; j < slices; j++)
            {
                float t0 = 2 * MathF.PI * j / slices;
                float t1 = 2 * MathF.PI * (j + 1) / slices;
                var p00 = CapVertex(phi0, t0, false);
                var p10 = CapVertex(phi1, t0, false);
                var p01 = CapVertex(phi0, t1, false);
                var p11 = CapVertex(phi1, t1, false);
                AddTri(p00, p10, p11);
                AddTri(p00, p11, p01);
            }
        }

        // Cylinder body
        for (int j = 0; j < slices; j++)
        {
            float t0 = 2 * MathF.PI * j / slices;
            float t1 = 2 * MathF.PI * (j + 1) / slices;
            float x0 = MathF.Cos(t0), z0 = MathF.Sin(t0);
            float x1 = MathF.Cos(t1), z1 = MathF.Sin(t1);
            float yB = 0 + r;
            float yT = Height - r;

            var bl = new JVector(x0 * r, yB, z0 * r);
            var br = new JVector(x1 * r, yB, z1 * r);
            var tl = new JVector(x0 * r, yT, z0 * r);
            var tr = new JVector(x1 * r, yT, z1 * r);

            AddTri(bl, br, tr);
            AddTri(bl, tr, tl);
        }

        AabbMin = new JVector(-r, 0f, -r);
        AabbMax = new JVector(r, Height, r);
        TriangleMesh = new TriangleMesh(triangles, ignoreDegenerated: true);
    }
}