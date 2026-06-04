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
    public class BoneWeight
    {
        public int VertexIndex;
        public float Weight;
    }

    public class BoneData
    {
        public string Name;
        public Matrix4x4 OffsetMatrix;        // mesh space → bone space
        public List<BoneWeight> Weights = new();
    }

    public class SkeletonNode
    {
        public string Name;
        public Matrix4x4 LocalTransform;
        public List<SkeletonNode> Children = new();
    }

    public class MeshFile : ResourceFile
    {
        public List<(float[] Vertices, int MaterialIndex, List<BoneData> Bones)> SubMeshes = new();
        public SkeletonNode RootNode;          // the shared skeleton hierarchy

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
                           PostProcessSteps.JoinIdenticalVertices));
                // NOTE: PreTransformVertices removed — it destroys skeleton data

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

                // Build the shared skeleton hierarchy from the scene node tree
                RootNode = BuildSkeletonNode(scene->MRootNode);

                // Process meshes
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

                            var pos = mesh->MVertices[index];
                            verts.Add(pos.X * unitScale);
                            verts.Add(pos.Y * unitScale);
                            verts.Add(pos.Z * unitScale);

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

                    // Collect bone data for this mesh
                    var bones = new List<BoneData>();
                    for (uint b = 0; b < mesh->MNumBones; b++)
                    {
                        var bone = mesh->MBones[b];
                        var boneData = new BoneData
                        {
                            Name = bone->MName.AsString,
                            OffsetMatrix = ToMatrix4x4(bone->MOffsetMatrix)
                        };

                        for (uint w = 0; w < bone->MNumWeights; w++)
                        {
                            boneData.Weights.Add(new BoneWeight
                            {
                                VertexIndex = (int)bone->MWeights[w].MVertexId,
                                Weight = bone->MWeights[w].MWeight
                            });
                        }

                        bones.Add(boneData);
                    }

                    SubMeshes.Add((verts.ToArray(), materialIndex, bones));
                }

                assimp.FreeScene(scene);
            }
        }

        private unsafe SkeletonNode BuildSkeletonNode(Node* node)
        {
            if (node == null) return null;

            var skNode = new SkeletonNode
            {
                Name = node->MName.AsString,
                LocalTransform = ToMatrix4x4(node->MTransformation)
            };

            for (uint i = 0; i < node->MNumChildren; i++)
                skNode.Children.Add(BuildSkeletonNode(node->MChildren[i]));

            return skNode;
        }

        private static Matrix4x4 ToMatrix4x4(System.Numerics.Matrix4x4 m) => m; // Assimp uses row-major, System.Numerics too — direct cast is fine
    }
}