using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using OssianForge.Engine.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using File = System.IO.File;
using Math = System.Math;

namespace OssianForge.Engine.Resources.MeshFiles
{
    public class BoneWeight
    {
        public int VertexIndex; // flat VBO index (after face unrolling)
        public float Weight;
    }

    public class BoneData
    {
        public string Name;
        public Matrix4x4 OffsetMatrix;
        public List<BoneWeight> Weights = new();
    }

    public class SkeletonNode
    {
        public string Name;
        public Matrix4x4 LocalTransform;
        public List<SkeletonNode> Children = new();
    }

    public class MeshFile : Resource
    {
        public List<(float[] Vertices, int MaterialIndex, List<BoneData> Bones)> SubMeshes = new();
        public SkeletonNode RootNode;
        public string Path;

        private static readonly string[] PivotSuffixes = {
            "_$AssimpFbx$_Translation",
            "_$AssimpFbx$_PreRotation",
            "_$AssimpFbx$_Rotation",
            "_$AssimpFbx$_PostRotation",
            "_$AssimpFbx$_Scaling",
            "_$AssimpFbx$_GeometricTranslation",
            "_$AssimpFbx$_GeometricRotation",
            "_$AssimpFbx$_GeometricScaling",
        };

        public MeshFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            base.Load();
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;

            using var assimp = Assimp.GetApi();

            unsafe
            {
                var scene = assimp.ImportFile(globalPath,
                    (uint)(PostProcessSteps.Triangulate |
                           PostProcessSteps.GenerateNormals |
                           PostProcessSteps.JoinIdenticalVertices |
                           PostProcessSteps.LimitBoneWeights));

                if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
                {
                    string assimpError = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load model: {globalPath}\nAssimp error: {assimpError}");
                }

                float unitScale = 1f;
                var metadata = scene->MMetaData;
                if (metadata != null)
                {
                    for (uint k = 0; k < metadata->MNumProperties; k++)
                    {
                        string key = metadata->MKeys[k].AsString;
                        var entry = metadata->MValues[k];

                        if (key == "UnitScaleFactor")
                        {
                            double raw = entry.MType switch
                            {
                                MetadataType.Double => *(double*)entry.MData,
                                MetadataType.Float => *(float*)entry.MData,
                                MetadataType.Int32 => *(int*)entry.MData,
                                MetadataType.Int64 => *(long*)entry.MData,
                                _ => 1.0
                            };
                            unitScale = (float)(raw * 0.01);
                        }
                    }
                }

                if (unitScale == 1f && scene->MNumMeshes > 0)
                {
                    var firstMesh = scene->MMeshes[0];
                    if (firstMesh->MNumVertices > 0)
                    {
                        var p = firstMesh->MVertices[0];
                        float maxCoord = Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z)));
                        if (maxCoord > 10f)
                            unitScale = 0.01f;
                    }
                }

                RootNode = BuildSkeletonNode(scene->MRootNode);

                if (unitScale != 1f)
                    ScaleSkeletonTranslations(RootNode, unitScale);

                for (uint m = 0; m < scene->MNumMeshes; m++)
                {
                    var mesh = scene->MMeshes[m];
                    int materialIndex = (int)mesh->MMaterialIndex;
                    var verts = new List<float>();

                    int originalVertexCount = (int)mesh->MNumVertices;
                    var originalToFlat = new List<int>[originalVertexCount];
                    for (int i = 0; i < originalVertexCount; i++)
                        originalToFlat[i] = new List<int>();

                    int flatIndex = 0;
                    for (uint i = 0; i < mesh->MNumFaces; i++)
                    {
                        var face = mesh->MFaces[i];
                        for (uint j = 0; j < face.MNumIndices; j++)
                        {
                            uint index = face.MIndices[j];
                            originalToFlat[index].Add(flatIndex++);

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
                            else { verts.Add(0f); verts.Add(1f); verts.Add(0f); }

                            if (mesh->MTextureCoords[0] != null)
                            {
                                var uv = mesh->MTextureCoords[0][index];
                                verts.Add(uv.X);
                                verts.Add(uv.Y);
                            }
                            else { verts.Add(0f); verts.Add(0f); }
                        }
                    }

                    var bones = new List<BoneData>();
                    for (uint b = 0; b < mesh->MNumBones; b++)
                    {
                        var bone = mesh->MBones[b];
                        var offsetMatrix = ToMatrix4x4(bone->MOffsetMatrix);

                        if (unitScale != 1f)
                        {
                            offsetMatrix.M41 *= unitScale;
                            offsetMatrix.M42 *= unitScale;
                            offsetMatrix.M43 *= unitScale;
                        }

                        var boneData = new BoneData
                        {
                            Name = bone->MName.AsString,
                            OffsetMatrix = offsetMatrix
                        };

                        for (uint w = 0; w < bone->MNumWeights; w++)
                        {
                            int originalVi = (int)bone->MWeights[w].MVertexId;
                            float weight = bone->MWeights[w].MWeight;
                            foreach (int flatVi in originalToFlat[originalVi])
                                boneData.Weights.Add(new BoneWeight { VertexIndex = flatVi, Weight = weight });
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
            string rawName = node->MName.AsString;
            string realName = StripPivotSuffix(rawName);

            // Assimp stores MTransformation in column-vector convention (row-major memory).
            // Transpose → row-vector convention (System.Numerics).
            Matrix4x4 combinedLocal = Matrix4x4.Transpose(node->MTransformation);
            var realChildren = new List<SkeletonNode>();

            for (uint i = 0; i < node->MNumChildren; i++)
            {
                Node* child = node->MChildren[i];
                string childReal = StripPivotSuffix(child->MName.AsString);

                if (childReal == realName)
                    combinedLocal = AbsorbPivotChain(child, combinedLocal, realChildren);
                else
                    realChildren.Add(BuildSkeletonNode(child));
            }

            var skNode = new SkeletonNode
            {
                Name = realName,
                LocalTransform = combinedLocal
            };
            skNode.Children.AddRange(realChildren);
            return skNode;
        }

        /// <summary>
        /// Absorbs a chain of FBX pivot helper nodes (all sharing the same real bone name)
        /// into a single combined local transform.
        ///
        /// In row-vector convention (System.Numerics), transforms compose left-to-right:
        ///   v' = v * A * B   means "apply A first, then B"
        ///   WorldChild = WorldParent * LocalChild
        ///
        /// The FBX pivot chain in tree order (each node is a LOCAL child of the previous):
        ///   Bone → Bone_Translation → Bone_PreRotation → Bone_Rotation → Bone_PostRotation → Bone_Scale
        ///
        /// Combined local in row-vector (each successive pivot RIGHT-appended):
        ///   Combined = Local_Bone * Local_Trans * Local_PreRot * Local_Rot * Local_PostRot * Local_Scale
        ///
        /// Therefore: accumulated = accumulated * pivotLocal  (RIGHT-append each new pivot)
        ///
        /// History note: a previous version used LEFT-prepend (pivotLocal * accumulated),
        /// based on a transposition derivation that double-counted the transpose step.
        /// Left-prepend gives the reverse order, producing mirrored/exploded limbs on
        /// animations that exercise the full 3-axis rotation range (e.g. jump, overhead).
        /// Walk cycles mostly swing arms forward/back and hide the error. Jump does not.
        /// </summary>
        private unsafe Matrix4x4 AbsorbPivotChain(Node* pivotNode, Matrix4x4 accumulated,
                                                   List<SkeletonNode> realChildren)
        {
            string pivotReal = StripPivotSuffix(pivotNode->MName.AsString);

            // Transpose: Assimp column-vector (row-major) → row-vector (System.Numerics)
            Matrix4x4 pivotLocal = Matrix4x4.Transpose(pivotNode->MTransformation);

            // RIGHT-append: each successive pivot in the chain is applied after the previous.
            accumulated = accumulated * pivotLocal;

            for (uint i = 0; i < pivotNode->MNumChildren; i++)
            {
                Node* child = pivotNode->MChildren[i];
                string childReal = StripPivotSuffix(child->MName.AsString);

                if (childReal == pivotReal)
                    accumulated = AbsorbPivotChain(child, accumulated, realChildren);
                else
                    realChildren.Add(BuildSkeletonNode(child));
            }

            return accumulated;
        }

        private static string StripPivotSuffix(string name)
        {
            foreach (var suffix in PivotSuffixes)
                if (name.EndsWith(suffix))
                    return name[..^suffix.Length];
            return name;
        }

        private static void ScaleSkeletonTranslations(SkeletonNode node, float scale)
        {
            node.LocalTransform.M41 *= scale;
            node.LocalTransform.M42 *= scale;
            node.LocalTransform.M43 *= scale;
            foreach (var child in node.Children)
                ScaleSkeletonTranslations(child, scale);
        }

        private static Matrix4x4 ToMatrix4x4(System.Numerics.Matrix4x4 m)
            => Matrix4x4.Transpose(m);
    }
}