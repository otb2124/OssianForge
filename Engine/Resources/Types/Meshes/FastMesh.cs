using OssianForge.Engine.Resources.Meshes;
using System;
using System.Collections.Generic;
using System.Numerics;

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

        // Layout: X Y Z  NX NY NZ  U V  (8 floats per vertex)

        public static FastMesh Triangle => new FastMesh("triangle", new float[]
        {
            // X      Y      Z      NX    NY    NZ    U     V
             0.0f,  0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 0.5f, 1.0f,
            -0.5f, -0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
             0.5f, -0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
        });

        public static FastMesh Plane => new FastMesh("plane", new float[]
        {
            // X      Y      Z      NX    NY    NZ    U     V
            -0.5f, 0.0f, -0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
             0.5f, 0.0f, -0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
             0.5f, 0.0f,  0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, 0.0f, -0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
             0.5f, 0.0f,  0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, 0.0f,  0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
        });

        public static FastMesh Cube => new FastMesh("cube", new float[]
        {
            // X      Y      Z      NX    NY    NZ    U     V
            // Front  (NZ = +1)
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
            // Back   (NZ = -1)
             0.5f, -0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 1.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 0.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f, 0.0f,-1.0f, 0.0f, 1.0f,
            // Left   (NX = -1)
            -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
            // Right  (NX = +1)
             0.5f, -0.5f,  0.5f,  1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
            // Top    (NY = +1)
            -0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
            // Bottom (NY = -1)
            -0.5f, -0.5f, -0.5f,  0.0f,-1.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  0.0f,-1.0f, 0.0f, 1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,-1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f,-1.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,-1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f,-1.0f, 0.0f, 0.0f, 1.0f,
        });

        public static FastMesh Pyramid => new FastMesh("pyramid", new float[]
        {
            // X      Y      Z      NX     NY     NZ     U     V
            // Base   (NY = -1, flat downward)
            -0.5f,  0.0f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.0f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.0f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f,  0.0f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.0f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f,  0.0f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
            // Front  face normal: (0, 0.5, 1) normalized
            -0.5f,  0.0f,  0.5f,  0.0f,  0.447f, 0.894f, 0.0f, 0.0f,
             0.5f,  0.0f,  0.5f,  0.0f,  0.447f, 0.894f, 1.0f, 0.0f,
             0.0f,  1.0f,  0.0f,  0.0f,  0.447f, 0.894f, 0.5f, 1.0f,
            // Back   face normal: (0, 0.5, -1) normalized
             0.5f,  0.0f, -0.5f,  0.0f,  0.447f,-0.894f, 0.0f, 0.0f,
            -0.5f,  0.0f, -0.5f,  0.0f,  0.447f,-0.894f, 1.0f, 0.0f,
             0.0f,  1.0f,  0.0f,  0.0f,  0.447f,-0.894f, 0.5f, 1.0f,
            // Left   face normal: (-1, 0.5, 0) normalized
            -0.5f,  0.0f, -0.5f, -0.894f, 0.447f, 0.0f,  0.0f, 0.0f,
            -0.5f,  0.0f,  0.5f, -0.894f, 0.447f, 0.0f,  1.0f, 0.0f,
             0.0f,  1.0f,  0.0f, -0.894f, 0.447f, 0.0f,  0.5f, 1.0f,
            // Right  face normal: (1, 0.5, 0) normalized
             0.5f,  0.0f,  0.5f,  0.894f, 0.447f, 0.0f,  0.0f, 0.0f,
             0.5f,  0.0f, -0.5f,  0.894f, 0.447f, 0.0f,  1.0f, 0.0f,
             0.0f,  1.0f,  0.0f,  0.894f, 0.447f, 0.0f,  0.5f, 1.0f,
        });

        public static FastMesh Quad => new FastMesh("quad", new float[]
        {
            // X      Y      Z      NX    NY    NZ    U     V
            -0.5f,  0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
            -0.5f, -0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
             0.5f, -0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
            -0.5f,  0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.0f,  0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
        });

        public static FastMesh ThickQuad => new FastMesh("thickquad", new float[]
        {
            // X      Y      Z      NX    NY    NZ    U     V
            // Front  (NZ = +1)
            -0.5f,  0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
            -0.5f, -0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
             0.5f, -0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
            -0.5f,  0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.005f,  0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
            // Back   (NZ = -1)
             0.5f,  0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 0.0f, 1.0f,
             0.5f, -0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 1.0f, 0.0f,
            -0.5f,  0.5f, -0.005f,  0.0f, 0.0f,-1.0f, 1.0f, 1.0f,
            // Top    (NY = +1)
            -0.5f,  0.5f, -0.005f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
            -0.5f,  0.5f,  0.005f,  0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
             0.5f,  0.5f,  0.005f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.005f,  0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
             0.5f,  0.5f,  0.005f,  0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
             0.5f,  0.5f, -0.005f,  0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
            // Bottom (NY = -1)
            -0.5f, -0.5f,  0.005f,  0.0f,-1.0f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.005f,  0.0f,-1.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, -0.005f,  0.0f,-1.0f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f,  0.005f,  0.0f,-1.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.005f,  0.0f,-1.0f, 0.0f, 1.0f, 1.0f,
             0.5f, -0.5f,  0.005f,  0.0f,-1.0f, 0.0f, 1.0f, 0.0f,
            // Left   (NX = -1)
            -0.5f,  0.5f, -0.005f, -1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.005f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.005f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f, -0.005f, -1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f,  0.005f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f,  0.005f, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
            // Right  (NX = +1)
             0.5f,  0.5f,  0.005f,  1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.005f,  1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.005f,  1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.005f,  1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
             0.5f, -0.5f, -0.005f,  1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.005f,  1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
        });

        public static FastMesh Cylinder => new FastMesh("cylinder", GenerateCylinder(0.5f, 1.0f, 24));
        public static FastMesh Ball => new FastMesh("ball", GenerateSphere(0.5f, 24, 24));

        // Helpers

        private static float[] V(float x, float y, float z, float nx, float ny, float nz, float u, float v)
            => new[] { x, y, z, nx, ny, nz, u, v };

        private static float[] GenerateCylinder(float radius, float height, int segments)
        {
            var verts = new List<float>();
            float halfH = height / 2f;

            for (int i = 0; i < segments; i++)
            {
                float a0 = 2 * MathF.PI * i / segments;
                float a1 = 2 * MathF.PI * (i + 1) / segments;

                float x0 = MathF.Cos(a0), z0 = MathF.Sin(a0); // unit circle
                float x1 = MathF.Cos(a1), z1 = MathF.Sin(a1);

                float u0 = (float)i / segments;
                float u1 = (float)(i + 1) / segments;

                // Side quad — normals point outward radially (no Y component)
                verts.AddRange(V(x0 * radius, -halfH, z0 * radius, x0, 0f, z0, u0, 0f));
                verts.AddRange(V(x1 * radius, -halfH, z1 * radius, x1, 0f, z1, u1, 0f));
                verts.AddRange(V(x1 * radius, halfH, z1 * radius, x1, 0f, z1, u1, 1f));
                verts.AddRange(V(x0 * radius, -halfH, z0 * radius, x0, 0f, z0, u0, 0f));
                verts.AddRange(V(x1 * radius, halfH, z1 * radius, x1, 0f, z1, u1, 1f));
                verts.AddRange(V(x0 * radius, halfH, z0 * radius, x0, 0f, z0, u0, 1f));

                // Top cap — normal points up
                float tu0x = 0.5f + MathF.Cos(a0) * 0.5f, tu0y = 0.5f + MathF.Sin(a0) * 0.5f;
                float tu1x = 0.5f + MathF.Cos(a1) * 0.5f, tu1y = 0.5f + MathF.Sin(a1) * 0.5f;
                verts.AddRange(V(0f, halfH, 0f, 0f, 1f, 0f, 0.5f, 0.5f));
                verts.AddRange(V(x0 * radius, halfH, z0 * radius, 0f, 1f, 0f, tu0x, tu0y));
                verts.AddRange(V(x1 * radius, halfH, z1 * radius, 0f, 1f, 0f, tu1x, tu1y));

                // Bottom cap — normal points down
                verts.AddRange(V(0f, -halfH, 0f, 0f, -1f, 0f, 0.5f, 0.5f));
                verts.AddRange(V(x1 * radius, -halfH, z1 * radius, 0f, -1f, 0f, tu1x, tu1y));
                verts.AddRange(V(x0 * radius, -halfH, z0 * radius, 0f, -1f, 0f, tu0x, tu0y));
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

                    float u0 = (float)j / slices, v0 = (float)i / stacks;
                    float u1 = (float)(j + 1) / slices, v1 = (float)(i + 1) / stacks;

                    var (p00, n00) = SphereVertex(radius, phi0, theta0);
                    var (p10, n10) = SphereVertex(radius, phi1, theta0);
                    var (p01, n01) = SphereVertex(radius, phi0, theta1);
                    var (p11, n11) = SphereVertex(radius, phi1, theta1);

                    verts.AddRange(V(p00.X, p00.Y, p00.Z, n00.X, n00.Y, n00.Z, u0, v0));
                    verts.AddRange(V(p10.X, p10.Y, p10.Z, n10.X, n10.Y, n10.Z, u0, v1)); 
                    verts.AddRange(V(p11.X, p11.Y, p11.Z, n11.X, n11.Y, n11.Z, u1, v1));

                    verts.AddRange(V(p00.X, p00.Y, p00.Z, n00.X, n00.Y, n00.Z, u0, v0));
                    verts.AddRange(V(p11.X, p11.Y, p11.Z, n11.X, n11.Y, n11.Z, u1, v1));
                    verts.AddRange(V(p01.X, p01.Y, p01.Z, n01.X, n01.Y, n01.Z, u1, v0));
                }
            }
            return verts.ToArray();
        }

        // Sphere normals are just the normalized position (point on unit sphere)
        private static (Vector3 pos, Vector3 normal) SphereVertex(float r, float phi, float theta)
        {
            var n = new Vector3(
                MathF.Cos(phi) * MathF.Cos(theta),
                MathF.Sin(phi),
                MathF.Cos(phi) * MathF.Sin(theta)
            );
            return (n * r, n); // normal = unit direction, pos = scaled by radius
        }
    }
}