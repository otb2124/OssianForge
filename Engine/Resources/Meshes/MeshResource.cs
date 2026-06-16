using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Resources.Meshes;
using Silk.NET.OpenGL;
using System.Numerics;

namespace OssianForge.Engine.Resources.Meshes
{

    public class MeshResource : Resource, IDisposable
    {
        public List<SubMeshResource> SubMeshes = new();
        public SkeletonNode Skeleton;
        public List<BoneData> AllBones = new();

        public Vector3 LocalAabbMin { get; private set; } = new Vector3(float.MaxValue);
        public Vector3 LocalAabbMax { get; private set; } = new Vector3(float.MinValue);

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
                    "fastmesh.thickquad" => FastMesh.ThickQuad,
                    _ => throw new Exception($"Unknown fast mesh: '{ResourceId}'")
                };

                SubMeshes.Add(new SubMeshResource(fast.Vertices, 0));
            }
            else
            {
                var meshFile = Engine.Resources.GetResourceFile<MeshFile>(ResourceId)
                ?? throw new Exception($"MeshFile not found: '{ResourceId}'");

                Skeleton = meshFile.RootNode;

                foreach (var (verts, matIndex, bones) in meshFile.SubMeshes)
                {
                    SubMeshes.Add(new SubMeshResource(verts, matIndex, bones: bones));

                    // Collect unique bones across all submeshes
                    foreach (var bone in bones)
                        if (!AllBones.Any(b => b.Name == bone.Name))
                            AllBones.Add(bone);
                }

                //Console.WriteLine($"[MESH] Loaded '{ResourceId}': {SubMeshes.Count} submeshes, {AllBones.Count} unique bones");
            }


            ComputeLocalAabb();
        }

        private void ComputeLocalAabb()
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var sub in SubMeshes)
            {
                var v = sub.RawVertices;
                const int stride = 8; // pos(3) + normal(3) + uv(2)
                for (int i = 0; i + stride <= v.Length; i += stride)
                {
                    var p = new Vector3(v[i], v[i + 1], v[i + 2]);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            LocalAabbMin = min;
            LocalAabbMax = max;
        }

        public void Draw()
        {
            foreach (SubMeshResource submesh in SubMeshes)
                submesh.Draw();
        }

        public void Dispose()
        {
            foreach (SubMeshResource submesh in SubMeshes)
                submesh.Dispose();
        }
    }


    public class SubMeshResource : IDisposable
    {
        protected uint _vao, _vbo, _boneIndexVbo, _boneWeightVbo;
        protected uint _vertexCount;
        public int MaterialIndex;

        public float[] RawVertices;
        public List<BoneData> Bones;

        public SubMeshResource(float[] vertices, int materialIndex = 0, List<BoneData> bones = null)
        {
            MaterialIndex = materialIndex;
            Bones = bones ?? new List<BoneData>();
            Init(vertices);
        }

        protected void Init(float[] vertices)
        {
            RawVertices = vertices;

            int stride = 8;
            _vertexCount = (uint)(vertices.Length / stride);

            var gl = Engine.Graphics.Batch.OpenGL;
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

            gl.EnableVertexAttribArray(1);
            unsafe { gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }
            offset += 3;

            uint uvLoc = 2u;
            gl.EnableVertexAttribArray(uvLoc);
            unsafe { gl.VertexAttribPointer(uvLoc, 2, GLEnum.Float, false, (uint)(stride * sizeof(float)), (void*)(offset * sizeof(float))); }


            // Upload bone influences (indices + weights) per vertex into a second VBO
            // Layout: each vertex gets 4 bone indices (int) + 4 bone weights (float)
            if (Bones != null && Bones.Count > 0)
                UploadBoneData(gl);

            gl.BindVertexArray(0);
        }

        private unsafe void UploadBoneData(GL gl)
        {
            const int MAX_BONES_PER_VERTEX = 4;

            var boneIndices = new float[_vertexCount * MAX_BONES_PER_VERTEX];
            var boneWeights = new float[_vertexCount * MAX_BONES_PER_VERTEX];
            var counts = new int[_vertexCount];

            for (int b = 0; b < Bones.Count; b++)
            {
                foreach (var w in Bones[b].Weights)
                {
                    int vi = w.VertexIndex;
                    int slot = counts[vi];
                    if (vi >= _vertexCount || slot >= MAX_BONES_PER_VERTEX) continue;
                    boneIndices[vi * MAX_BONES_PER_VERTEX + slot] = b;
                    boneWeights[vi * MAX_BONES_PER_VERTEX + slot] = w.Weight;
                    counts[vi]++;
                }
            }

            // Bone indices — location 3
            _boneIndexVbo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _boneIndexVbo);
            fixed (float* ptr = boneIndices)
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(boneIndices.Length * sizeof(float)),
                    ptr, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(3);
            gl.VertexAttribPointer(3, MAX_BONES_PER_VERTEX, GLEnum.Float, false,
                (uint)(MAX_BONES_PER_VERTEX * sizeof(float)), (void*)0);

            // Bone weights — location 4
            _boneWeightVbo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _boneWeightVbo);
            fixed (float* ptr = boneWeights)
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(boneWeights.Length * sizeof(float)),
                    ptr, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(4);
            gl.VertexAttribPointer(4, MAX_BONES_PER_VERTEX, GLEnum.Float, false,
                (uint)(MAX_BONES_PER_VERTEX * sizeof(float)), (void*)0);
        }

        public virtual void Draw()
        {
            Engine.Graphics.Batch.OpenGL.BindVertexArray(_vao);
            Engine.Graphics.Batch.OpenGL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
        }

        public virtual void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.DeleteVertexArray(_vao);
            gl.DeleteBuffer(_vbo);
            if (_boneIndexVbo != 0) gl.DeleteBuffer(_boneIndexVbo);
            if (_boneWeightVbo != 0) gl.DeleteBuffer(_boneWeightVbo);
        }
    }
}