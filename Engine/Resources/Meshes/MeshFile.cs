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

        // Saved after Load() — the FBX→meter scale factor read from scene metadata.
        // Vertex positions are scaled by this, so all bone data must be scaled too.
        private float _unitScale = 1f;

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

                // Save so bone-data loaders can use the same scale.
                _unitScale = unitScale;

                RootNode = BuildSkeletonNode(scene->MRootNode);
                // Scale skeleton node translations to meters so they match vertex positions.
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

                        // OffsetMatrix translation is in FBX native units (e.g. cm for Mixamo).
                        // Vertex positions were scaled to meters by unitScale above.
                        // Scale the translation part of OffsetMatrix to match.
                        // In row-vector System.Numerics the translation lives in row 4: M41, M42, M43.
                        offsetMatrix.M41 *= unitScale;
                        offsetMatrix.M42 *= unitScale;
                        offsetMatrix.M43 *= unitScale;

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
        /// FBX pivot chain in Assimp node hierarchy (parent → child order):
        ///   Bone  →  Bone_Translation  →  Bone_PreRotation  →  Bone_Rotation  →  Bone_PostRotation
        ///
        /// Each node's MTransformation is LOCAL to its immediate parent.
        /// In column-vector convention the combined global is:
        ///   G = G_parent_col * L_bone_col * L_trans_col * L_prerot_col * L_rot_col * L_post_col
        ///
        /// We want the single LOCAL transform of the real bone (relative to its parent) to be:
        ///   L_combined_col = L_bone_col * L_trans_col * L_prerot_col * L_rot_col * L_post_col
        ///
        /// After transposing each piece into row-vector (System.Numerics) convention:
        ///   L_combined_rv = L_post_rv * L_rot_rv * L_prerot_rv * L_trans_rv * L_bone_rv
        ///
        /// Building left-to-right: start with L_bone_rv, then for each successive child pivot
        /// we prepend it to the LEFT (because in row-vector: v * L_combined = v * L_post * ... * L_bone).
        ///   accumulated = pivotLocal_rv * accumulated
        ///
        /// Wait — that is the WRONG mental model. Think of it differently in row-vector:
        ///   v_final = v * L_bone * L_trans * L_prerot * L_rot * L_post
        ///
        /// Starting with accumulated = L_bone_rv, appending each child pivot to the RIGHT:
        ///   accumulated = accumulated * L_trans_rv  → v * L_bone * L_trans
        ///   accumulated = accumulated * L_prerot_rv → v * L_bone * L_trans * L_prerot
        ///   ...
        ///
        /// Therefore each new pivot must be RIGHT-multiplied (appended).
        /// </summary>
        private unsafe Matrix4x4 AbsorbPivotChain(Node* pivotNode, Matrix4x4 accumulated,
                                                   List<SkeletonNode> realChildren)
        {
            string pivotReal = StripPivotSuffix(pivotNode->MName.AsString);

            // Transpose: Assimp column-vector (row-major memory) → row-vector (System.Numerics)
            Matrix4x4 pivotLocal = Matrix4x4.Transpose(pivotNode->MTransformation);

            // RIGHT-multiply: each successive child pivot is applied AFTER the accumulated parent.
            // In row-vector convention  v * accumulated * pivotLocal  means:
            //   accumulated is applied first, then pivotLocal — which is exactly the
            //   parent-before-child order that matches the Assimp node hierarchy.
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

        /// <summary>
        /// Recursively scales the translation component of every skeleton node's
        /// LocalTransform by <paramref name="scale"/> so that node translations
        /// are in the same units as the (already-scaled) vertex positions.
        /// In System.Numerics row-vector convention the translation is in M41, M42, M43.
        /// </summary>
        private static void ScaleSkeletonTranslations(SkeletonNode node, float scale)
        {
            if (scale == 1f) return;
            node.LocalTransform.M41 *= scale;
            node.LocalTransform.M42 *= scale;
            node.LocalTransform.M43 *= scale;
            foreach (var child in node.Children)
                ScaleSkeletonTranslations(child, scale);
        }

        private static string StripPivotSuffix(string name)
        {
            foreach (var suffix in PivotSuffixes)
                if (name.EndsWith(suffix))
                    return name[..^suffix.Length];
            return name;
        }

        private static Matrix4x4 ToMatrix4x4(System.Numerics.Matrix4x4 m)
            => Matrix4x4.Transpose(m);
    }
}