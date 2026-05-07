using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Resources.Meshes;
using Silk.NET.OpenGL;

namespace OssianForge.Engine.Resources.Meshes
{
    public class MeshResource : Resource, IDisposable
    {

        public List<SubMeshResource> SubMeshes = new();

        public string ResourceId;

        public MeshResource(string id, string meshId) 
        {
            Id = id;
            ResourceId = meshId;
        }

        public override void Load()
        {
            if (ResourceId.StartsWith("fastmesh."))
            {
                FastMesh fast = ResourceId switch
                {
                    "fastmesh.triangle" => FastMesh.Triangle,
                    "fastmesh.plane" => FastMesh.Plane,
                    "fastmesh.cube" => FastMesh.Cube,
                    "fastmesh.pyramid" => FastMesh.Pyramid,
                    "fastmesh.cylinder" => FastMesh.Cylinder,
                    "fastmesh.ball" => FastMesh.Ball,
                    "fastmesh.quad" => FastMesh.Quad,
                    _ => throw new Exception($"Unknown fast mesh: '{ResourceId}'")
                };

                bool hasUV = ResourceId is "fastmesh.quad" or "fastmesh.plane";
                bool hasNormals = ResourceId is "fastmesh.plane";
                SubMeshes.Add(new SubMeshResource(fast.Vertices, 0, hasUV, hasNormals));
            }
            else
            {
                var meshFile = Engine.Resources.GetResourceFile(ResourceId) as MeshFile
                    ?? throw new Exception($"MeshFile not found: '{ResourceId}'");

                foreach (var (verts, matIndex) in meshFile.SubMeshes)
                    SubMeshes.Add(new SubMeshResource(verts, matIndex, hasUV: true, hasNormals: true));
            }
        }


        public void Draw()
        {
            foreach (SubMeshResource submesh in SubMeshes)
            {
                submesh.Draw();
            }
        }

        public void Dispose()
        {
            foreach (SubMeshResource submesh in SubMeshes)
            {
                submesh.Dispose();
            }
        }
    }


    public class SubMeshResource : IDisposable
    {

        protected uint _vao, _vbo;
        protected uint _vertexCount;
        public int MaterialIndex;

        public SubMeshResource(float[] vertices, int materialIndex = 0, bool hasUV = false, bool hasNormals = false)
        {
            MaterialIndex = materialIndex;
            Init(vertices, hasUV, hasNormals);
        }


        protected void Init(float[] vertices, bool hasUV, bool hasNormals)
        {
            int stride = 3;
            if (hasNormals) stride += 3;
            if (hasUV) stride += 2;
            _vertexCount = (uint)(vertices.Length / stride);

            var gl = Engine.Graphics.OpenGL;
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* ptr = vertices)
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(vertices.Length * sizeof(float)),
                        ptr, BufferUsageARB.StaticDraw);
            }

            int offset = 0;

            gl.EnableVertexAttribArray(0);
            unsafe { gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
            offset += 3;

            if (hasNormals)
            {
                gl.EnableVertexAttribArray(1);
                unsafe { gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
                offset += 3;
            }

            if (hasUV)
            {
                uint uvLoc = hasNormals ? 2u : 1u;
                gl.EnableVertexAttribArray(uvLoc);
                unsafe { gl.VertexAttribPointer(uvLoc, 2, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
            }

            gl.BindVertexArray(0);
        }

        public virtual void Draw()
        {
            Engine.Graphics.OpenGL.BindVertexArray(_vao);
            Engine.Graphics.OpenGL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
        }

        public virtual void Dispose()
        {
            Engine.Graphics.OpenGL.DeleteVertexArray(_vao);
            Engine.Graphics.OpenGL.DeleteBuffer(_vbo);
        }
    }
}
