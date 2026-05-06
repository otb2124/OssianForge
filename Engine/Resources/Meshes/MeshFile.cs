using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using OssianForge.Engine.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using File = System.IO.File;

namespace OssianForge.Engine.Resources.MeshFiles
{
    public class MeshFile : ResourceFile
    {

        public List<(float[] Vertices, int MaterialIndex)> SubMeshes = new();

        public MeshFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;

            SanitizeMtl(globalPath);

            using var assimp = Assimp.GetApi();
            

            unsafe
            {
                var scene = assimp.ImportFile(globalPath,
                (uint)(PostProcessSteps.Triangulate |
                       PostProcessSteps.FlipUVs |
                       PostProcessSteps.GenerateNormals |
                       PostProcessSteps.JoinIdenticalVertices));

                if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
                {
                    string assimpError = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load model: {globalPath}\nAssimp error: {assimpError}");
                }


                for (uint m = 0; m < scene->MNumMeshes; m++)
                {
                    var mesh = scene->MMeshes[m];
                    int materialIndex = (int)mesh->MMaterialIndex;
                    var verts = new List<float>();

                    for (uint i = 0; i < mesh->MNumFaces; i++)
                    {
                        var face = mesh->MFaces[i];
                        for (uint j = 0; j < face.MNumIndices; j++)
                        {
                            uint index = face.MIndices[j];

                            // Position
                            var pos = mesh->MVertices[index];
                            verts.Add(pos.X);
                            verts.Add(pos.Y);
                            verts.Add(pos.Z);

                            // Normal
                            if (mesh->MNormals != null)
                            {
                                var n = mesh->MNormals[index];
                                verts.Add(n.X);
                                verts.Add(n.Y);
                                verts.Add(n.Z);
                            }
                            else
                            {
                                verts.Add(0f);
                                verts.Add(1f);
                                verts.Add(0f);
                            }

                            // UV
                            if (mesh->MTextureCoords[0] != null)
                            {
                                var uv = mesh->MTextureCoords[0][index];
                                verts.Add(uv.X);
                                verts.Add(uv.Y);
                            }
                            else
                            {
                                verts.Add(0f);
                                verts.Add(0f);
                            }
                        }
                    }

                    SubMeshes.Add((verts.ToArray(), materialIndex));
                }
                assimp.FreeScene(scene);
            }
        }

        private void SanitizeMtl(string objPath)
        {
            string mtlPath = System.IO.Path.ChangeExtension(objPath, ".mtl");
            if (!File.Exists(mtlPath)) return;

            var allowed = new[] { "#", "newmtl", "Ka", "Kd", "Ks", "Ke", "Ni", "d", "illum" };

            var cleaned = File.ReadAllLines(mtlPath)
                .Where(l => string.IsNullOrWhiteSpace(l) || allowed.Any(p => l.TrimStart().StartsWith(p)))
                .ToArray();

            File.WriteAllLines(mtlPath, cleaned);
        }


    }
}
