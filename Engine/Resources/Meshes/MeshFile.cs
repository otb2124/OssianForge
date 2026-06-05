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

    public class MeshFile : ResourceFile
    {
        public List<(float[] Vertices, int MaterialIndex, List<BoneData> Bones)> SubMeshes = new();
        public SkeletonNode RootNode;

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
            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;

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
                    //Console.WriteLine($"[MESH] Metadata keys ({metadata->MNumProperties}):");
                    for (uint k = 0; k < metadata->MNumProperties; k++)
                    {
                        string key = metadata->MKeys[k].AsString;
                        var entry = metadata->MValues[k];

                        // Log every key so we can see what Assimp provides.
                        string valueStr = entry.MType switch
                        {
                            MetadataType.Double => (*(double*)entry.MData).ToString("F6"),
                            MetadataType.Float => (*(float*)entry.MData).ToString("F6"),
                            MetadataType.Int32 => (*(int*)entry.MData).ToString(),
                            MetadataType.Int64 => (*(long*)entry.MData).ToString(),
                            MetadataType.Bool => (*(bool*)entry.MData).ToString(),
                            _ => $"<type {entry.MType}>"
                        };
                        //Console.WriteLine($"  [{k}] \"{key}\" ({entry.MType}) = {valueStr}");

                        if (key == "UnitScaleFactor")
                        {
                            // Assimp may store this as Double, Float, or Int32 depending
                            // on the FBX exporter. Handle all three.
                            double raw = entry.MType switch
                            {
                                MetadataType.Double => *(double*)entry.MData,
                                MetadataType.Float => *(float*)entry.MData,
                                MetadataType.Int32 => *(int*)entry.MData,
                                MetadataType.Int64 => *(long*)entry.MData,
                                _ => 1.0
                            };
                            // UnitScaleFactor is centimetres-per-unit in the FBX sense:
                            //   1   → file is already in metres  (scale = 1.0)
                            //   100 → file is in centimetres     (scale = 0.01)
                            unitScale = (float)(raw * 0.01);
                            //Console.WriteLine($"[MESH] UnitScaleFactor={raw} → unitScale={unitScale}");
                        }
                    }
                }
                else
                {
                    //Console.WriteLine("[MESH] No metadata found in scene.");
                }

                // Safety-net: if unitScale is still 1 (nothing found or factor was 1),
                // check whether the first vertex position looks like centimetres by
                // seeing if the mesh is unreasonably large (>10 units on any axis).
                // Mixamo FBX without metadata is always centimetres.
                if (unitScale == 1f && scene->MNumMeshes > 0)
                {
                    var firstMesh = scene->MMeshes[0];
                    if (firstMesh->MNumVertices > 0)
                    {
                        var p = firstMesh->MVertices[0];
                        float maxCoord = Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z)));
                        if (maxCoord > 10f)
                        {
                            unitScale = 0.01f;
                            //Console.WriteLine($"[MESH] No UnitScaleFactor found but first vertex coord is {maxCoord:F1}" + $" — assuming centimetres, applying unitScale=0.01");
                        }
                        else
                        {
                            //Console.WriteLine($"[MESH] No UnitScaleFactor found, first vertex coord={maxCoord:F3}, keeping unitScale=1");
                        }
                    }
                }

                //Console.WriteLine($"[MESH] Final unitScale = {unitScale}");

                RootNode = BuildSkeletonNode(scene->MRootNode);
                // Scale every skeleton-node translation so bone positions are in the
                // same units as the (already-scaled) vertex positions.
                // In System.Numerics row-vector layout, translation is in M41/M42/M43.
                if (unitScale != 1f)
                    ScaleSkeletonTranslations(RootNode, unitScale);

                for (uint m = 0; m < scene->MNumMeshes; m++)
                {
                    var mesh = scene->MMeshes[m];
                    int materialIndex = (int)mesh->MMaterialIndex;
                    var verts = new List<float>();

                    // Map each original Assimp vertex index to all flat VBO positions it was
                    // emitted at during face unrolling. Bone weights from Assimp reference
                    // original indices; we need them in flat VBO space for UploadBoneData.
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

                        // OffsetMatrix encodes the inverse bind-pose in the FBX file's
                        // native units. Its translation column must match the scaled vertices.
                        // In System.Numerics row-vector: translation lives in M41/M42/M43.
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

            // Each Assimp node's MTransformation is stored row-major (column-vector convention).
            // Transposing it gives us the equivalent row-vector matrix (System.Numerics convention).
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
        /// Absorbs a chain of FBX pivot helper nodes (all belonging to the same real bone)
        /// into a single combined local transform, and surfaces any real child bones it
        /// discovers along the way into <paramref name="realChildren"/>.
        ///
        /// FBX pivot chain (Assimp column-vector order, parent → child):
        ///   Bone  →  Bone_Translation  →  Bone_PreRotation  →  Bone_Rotation  → ...
        /// Each child's transform is LOCAL to its parent. The full combined local is:
        ///   Trans_col * PreRot_col * Rot_col * PostRot_col * Scale_col   (column-vector)
        ///
        /// After Transpose (→ row-vector, System.Numerics convention), the equivalent is:
        ///   Scale_rv * PostRot_rv * Rot_rv * PreRot_rv * Trans_rv
        /// which is built by prepending each new pivot to the LEFT:
        ///   accumulated = pivotLocal * accumulated   ← NEW pivot on LEFT
        ///
        /// The previous code used  accumulated = accumulated * pivotLocal  (right-append),
        /// which is the WRONG order in row-vector space and produced incorrect bone rotations,
        /// causing the Y-translation (~209 cm) to bleed into X/Z through the rotation part
        /// of the offset matrix and producing the "mountain" deformation.
        /// </summary>
        private unsafe Matrix4x4 AbsorbPivotChain(Node* pivotNode, Matrix4x4 accumulated,
                                                   List<SkeletonNode> realChildren)
        {
            string pivotReal = StripPivotSuffix(pivotNode->MName.AsString);

            // Transpose: Assimp row-major (col-vector) → row-vector (System.Numerics)
            Matrix4x4 pivotLocal = Matrix4x4.Transpose(pivotNode->MTransformation);

            // BUG FIX: prepend the new pivot on the LEFT so the pivot chain builds up
            // in the correct order for row-vector convention.
            // Previous: accumulated = accumulated * pivotLocal  (wrong — right-append)
            // Correct:  accumulated = pivotLocal * accumulated  (left-prepend)
            accumulated = pivotLocal * accumulated;

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

        /// <summary>
        /// Recursively scales the translation part of every skeleton node's LocalTransform.
        /// In System.Numerics row-vector convention, translation is stored in M41/M42/M43.
        /// Must be called after BuildSkeletonNode so that pivot chains are already collapsed.
        /// </summary>
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