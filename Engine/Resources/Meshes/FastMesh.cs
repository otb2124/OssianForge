using OssianForge.Engine.Resources.Meshes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Resources.Meshes
{
    public struct FastMesh
    {
        public string Id;
        public float[] Vertices;

        public FastMesh(string id, float[] vertices)
        {
            Id = id;
            Vertices = vertices;
        }

        public SubMeshResource ToMesh() => new SubMeshResource(Vertices);

        public static FastMesh Triangle => new FastMesh("triangle", new float[]
        {
         0.0f,  0.5f, 0.0f,
        -0.5f, -0.5f, 0.0f,
         0.5f, -0.5f, 0.0f,
        });

        public static FastMesh Plane => new FastMesh("plane", new float[]
        {
            // x      y      z     nx   ny   nz    u     v
            -0.5f, 0.0f, -0.5f,  0f,  1f,  0f,  0.0f, 0.0f,
             0.5f, 0.0f, -0.5f,  0f,  1f,  0f,  1.0f, 0.0f,
             0.5f, 0.0f,  0.5f,  0f,  1f,  0f,  1.0f, 1.0f,
            -0.5f, 0.0f, -0.5f,  0f,  1f,  0f,  0.0f, 0.0f,
             0.5f, 0.0f,  0.5f,  0f,  1f,  0f,  1.0f, 1.0f,
            -0.5f, 0.0f,  0.5f,  0f,  1f,  0f,  0.0f, 1.0f,
        });

        public static FastMesh Cube => new FastMesh("cube", new float[]
        {
        // Front
        -0.5f, -0.5f,  0.5f,  0.5f, -0.5f,  0.5f,  0.5f,  0.5f,  0.5f,
        -0.5f, -0.5f,  0.5f,  0.5f,  0.5f,  0.5f, -0.5f,  0.5f,  0.5f,
        // Back
         0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f,  0.5f, -0.5f,
         0.5f, -0.5f, -0.5f, -0.5f,  0.5f, -0.5f,  0.5f,  0.5f, -0.5f,
        // Left
        -0.5f, -0.5f, -0.5f, -0.5f, -0.5f,  0.5f, -0.5f,  0.5f,  0.5f,
        -0.5f, -0.5f, -0.5f, -0.5f,  0.5f,  0.5f, -0.5f,  0.5f, -0.5f,
        // Right
         0.5f, -0.5f,  0.5f,  0.5f, -0.5f, -0.5f,  0.5f,  0.5f, -0.5f,
         0.5f, -0.5f,  0.5f,  0.5f,  0.5f, -0.5f,  0.5f,  0.5f,  0.5f,
        // Top
        -0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f,  0.5f, -0.5f,
        -0.5f,  0.5f,  0.5f,  0.5f,  0.5f, -0.5f, -0.5f,  0.5f, -0.5f,
        // Bottom
        -0.5f, -0.5f, -0.5f,  0.5f, -0.5f, -0.5f,  0.5f, -0.5f,  0.5f,
        -0.5f, -0.5f, -0.5f,  0.5f, -0.5f,  0.5f, -0.5f, -0.5f,  0.5f,
        });

        public static FastMesh Pyramid => new FastMesh("pyramid", new float[]
        {
        // Base
        -0.5f, 0.0f, -0.5f,  0.5f, 0.0f, -0.5f,  0.5f, 0.0f,  0.5f,
        -0.5f, 0.0f, -0.5f,  0.5f, 0.0f,  0.5f, -0.5f, 0.0f,  0.5f,
        // Front
        -0.5f, 0.0f,  0.5f,  0.5f, 0.0f,  0.5f,  0.0f, 1.0f,  0.0f,
        // Back
         0.5f, 0.0f, -0.5f, -0.5f, 0.0f, -0.5f,  0.0f, 1.0f,  0.0f,
        // Left
        -0.5f, 0.0f, -0.5f, -0.5f, 0.0f,  0.5f,  0.0f, 1.0f,  0.0f,
        // Right
         0.5f, 0.0f,  0.5f,  0.5f, 0.0f, -0.5f,  0.0f, 1.0f,  0.0f,
        });

        public static FastMesh Quad => new FastMesh("quad", new float[]
        {
            // X      Y     Z     U     V
            -0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
        });

        public static FastMesh Cylinder => new FastMesh("cylinder", GenerateCylinder(0.5f, 1.0f, 24));
        public static FastMesh Ball => new FastMesh("ball", GenerateSphere(0.5f, 24, 24));

        private static float[] GenerateCylinder(float radius, float height, int segments)
        {
            var verts = new List<float>();
            float halfH = height / 2f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2 * MathF.PI * i / segments;
                float a1 = 2 * MathF.PI * (i + 1) / segments;
                float x0 = MathF.Cos(a0) * radius, z0 = MathF.Sin(a0) * radius;
                float x1 = MathF.Cos(a1) * radius, z1 = MathF.Sin(a1) * radius;
                verts.AddRange(new[] { x0, -halfH, z0, x1, -halfH, z1, x1, halfH, z1 });
                verts.AddRange(new[] { x0, -halfH, z0, x1, halfH, z1, x0, halfH, z0 });
                verts.AddRange(new[] { 0f, halfH, 0f, x0, halfH, z0, x1, halfH, z1 });
                verts.AddRange(new[] { 0f, -halfH, 0f, x1, -halfH, z1, x0, -halfH, z0 });
            }
            return verts.ToArray();
        }

        private static float[] GenerateSphere(float radius, int stacks, int slices)
        {
            var verts = new List<float>();
            for (int i = 0; i < stacks; i++)
            {
                float phi0 = MathF.PI * i / stacks - MathF.PI / 2;
                float phi1 = MathF.PI * (i + 1) / stacks - MathF.PI / 2;
                for (int j = 0; j < slices; j++)
                {
                    float theta0 = 2 * MathF.PI * j / slices;
                    float theta1 = 2 * MathF.PI * (j + 1) / slices;
                    Vector3 v00 = SpherePoint(radius, phi0, theta0);
                    Vector3 v10 = SpherePoint(radius, phi1, theta0);
                    Vector3 v01 = SpherePoint(radius, phi0, theta1);
                    Vector3 v11 = SpherePoint(radius, phi1, theta1);
                    verts.AddRange(new[] { v00.X, v00.Y, v00.Z, v10.X, v10.Y, v10.Z, v11.X, v11.Y, v11.Z });
                    verts.AddRange(new[] { v00.X, v00.Y, v00.Z, v11.X, v11.Y, v11.Z, v01.X, v01.Y, v01.Z });
                }
            }
            return verts.ToArray();
        }

        private static Vector3 SpherePoint(float r, float phi, float theta) => new Vector3(
            r * MathF.Cos(phi) * MathF.Cos(theta),
            r * MathF.Sin(phi),
            r * MathF.Cos(phi) * MathF.Sin(theta)
        );
    }
}
