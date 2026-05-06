using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.MeshFiles;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class Model : NodeProperty, IDisposable
    {
        public List<Mesh> SubMeshes = new();
        public List<Material> Materials = new();

        public Model() { }

        public Model(FastMesh fastMesh)
        {
            AddFastMesh(fastMesh, hasUV: false, hasNormals: false);
        }

        public void AddMesh(string id)
        {
            if (id.StartsWith("fastmesh."))
            {
                FastMesh fast = id switch
                {
                    "fastmesh.triangle" => FastMesh.Triangle,
                    "fastmesh.plane" => FastMesh.Plane,
                    "fastmesh.cube" => FastMesh.Cube,
                    "fastmesh.pyramid" => FastMesh.Pyramid,
                    "fastmesh.cylinder" => FastMesh.Cylinder,
                    "fastmesh.ball" => FastMesh.Ball,
                    "fastmesh.quad" => FastMesh.Quad,
                    _ => throw new Exception($"Unknown fast mesh: '{id}'")
                };

                bool hasUV = id is "fastmesh.quad" or "fastmesh.plane";
                bool hasNormals = id is "fastmesh.plane";
                AddFastMesh(fast, hasUV, hasNormals);
            }
            else
            {
                var meshFile = Engine.Resources.GetResourceFile(id) as MeshFile
                    ?? throw new Exception($"MeshFile not found: '{id}'");

                foreach (var (verts, matIndex) in meshFile.SubMeshes)
                    SubMeshes.Add(new Mesh(verts, matIndex, hasUV: true, hasNormals: true));
            }
        }

        private void AddFastMesh(FastMesh fast, bool hasUV, bool hasNormals)
        {
            SubMeshes.Add(new Mesh(fast.Vertices, 0, hasUV, hasNormals));
        }

        public void AddMaterial(Material material) => Materials.Add(material);

        public void Draw(Matrix4x4 modelMatrix, Matrix4x4 view, Matrix4x4 proj)
        {
            int minMatIndex = SubMeshes.Count > 0 ? SubMeshes.Min(s => s.MaterialIndex) : 0;
            foreach (var subMesh in SubMeshes)
            {
                int matIndex = subMesh.MaterialIndex - minMatIndex;
                if (matIndex < 0 || matIndex >= Materials.Count) continue;
                Materials[matIndex].Apply(modelMatrix, view, proj);
                subMesh.Draw();
            }
        }

        public void Dispose()
        {
            foreach (var sub in SubMeshes) sub.Dispose();
        }
    }

}