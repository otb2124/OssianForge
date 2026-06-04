using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using OssianForge.Engine.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
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

            using var assimp = Assimp.GetApi();

            unsafe
            {
                var scene = assimp.ImportFile(globalPath,
                    (uint)(PostProcessSteps.Triangulate |
                           PostProcessSteps.GenerateNormals |
                           PostProcessSteps.JoinIdenticalVertices |
                           PostProcessSteps.PreTransformVertices));

                if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
                {
                    string assimpError = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load model: {globalPath}\nAssimp error: {assimpError}");
                }

                float unitScale = 0.01f;
                var metadata = scene->MMetaData;
                if (metadata != null)
                {
                    for (uint k = 0; k < metadata->MNumProperties; k++)
                    {
                        string key = metadata->MKeys[k].AsString;
                        if (key == "UnitScaleFactor")
                        {
                            var entry = metadata->MValues[k];
                            if (entry.MType == MetadataType.Double)
                                unitScale = (float)(*(double*)entry.MData) * 0.01f;
                            break;
                        }
                    }
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
                            verts.Add(pos.X * unitScale);
                            verts.Add(pos.Y * unitScale);
                            verts.Add(pos.Z * unitScale);

                            // Normal (direction — not scaled)
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
    }
}