using OssianForge.Content;
using OssianForge.Content.Meshes;
using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Numerics;
using Math = System.Math;

namespace OssianForge.Cooker.Importers;

/// <summary>
/// Port of MeshFile.Load(): same Assimp flags, unit-scale guessing, pivot-chain absorption and face unrolling,
/// so the cooked float[] layout is exactly what the renderer consumes today.
/// </summary>
internal sealed class MeshImporter : IImporter
{
    public AssetType Type => AssetType.Mesh;
    public int Version => 1;

    public byte[] Import(ImportContext ctx)
    {
        MeshData mesh;
        lock (AssimpCommon.Lock) mesh = ImportScene(ctx);
        return MeshCodec.Write(mesh);
    }

    private static unsafe MeshData ImportScene(ImportContext ctx)
    {
        using var assimp = Assimp.GetApi();

        var scene = assimp.ImportFile(ctx.FullPath,
            (uint)(PostProcessSteps.Triangulate |
                   PostProcessSteps.GenerateNormals |
                   PostProcessSteps.JoinIdenticalVertices |
                   PostProcessSteps.LimitBoneWeights));

        if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
            throw new Exception($"Failed to load model.\nAssimp error: {assimp.GetErrorStringS()}");

        try
        {
            float unitScale;
            if (ctx.Settings.UnitScale is { } forced)
            {
                unitScale = forced;
            }
            else
            {
                unitScale = AssimpCommon.ReadUnitScaleFactor(scene);

                // Fallback heuristic: no metadata and the first vertex is far from the origin -> centimeters.
                if (unitScale == 1f && scene->MNumMeshes > 0)
                {
                    var firstMesh = scene->MMeshes[0];
                    if (firstMesh->MNumVertices > 0)
                    {
                        var p = firstMesh->MVertices[0];
                        float maxCoord = Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z)));
                        if (maxCoord > 10f) unitScale = 0.01f;
                    }
                }
            }

            var result = new MeshData();
            result.Root = BuildSkeletonNode(scene->MRootNode);
            if (unitScale != 1f) ScaleSkeletonTranslations(result.Root, unitScale);

            var subMeshes = new List<SubMeshData>();
            for (uint m = 0; m < scene->MNumMeshes; m++)
            {
                var mesh = scene->MMeshes[m];
                var verts = new List<float>();

                int originalVertexCount = (int)mesh->MNumVertices;
                var originalToFlat = new List<int>[originalVertexCount];
                for (int i = 0; i < originalVertexCount; i++) originalToFlat[i] = new List<int>();

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

                var bones = new List<BoneInfo>();
                for (uint b = 0; b < mesh->MNumBones; b++)
                {
                    var bone = mesh->MBones[b];
                    var offsetMatrix = Matrix4x4.Transpose(bone->MOffsetMatrix);

                    if (unitScale != 1f)
                    {
                        offsetMatrix.M41 *= unitScale;
                        offsetMatrix.M42 *= unitScale;
                        offsetMatrix.M43 *= unitScale;
                    }

                    var weights = new List<BoneWeightEntry>();
                    for (uint w = 0; w < bone->MNumWeights; w++)
                    {
                        int originalVi = (int)bone->MWeights[w].MVertexId;
                        float weight = bone->MWeights[w].MWeight;
                        foreach (int flatVi in originalToFlat[originalVi])
                            weights.Add(new BoneWeightEntry { VertexIndex = flatVi, Weight = weight });
                    }

                    bones.Add(new BoneInfo
                    {
                        Name = bone->MName.AsString,
                        OffsetMatrix = offsetMatrix,
                        Weights = weights.ToArray()
                    });
                }

                subMeshes.Add(new SubMeshData
                {
                    MaterialIndex = (int)mesh->MMaterialIndex,
                    Vertices = verts.ToArray(),
                    Bones = bones.ToArray()
                });
            }

            result.SubMeshes = subMeshes.ToArray();
            return result;
        }
        finally
        {
            assimp.FreeScene(scene);
        }
    }

    private static unsafe SkeletonNodeData BuildSkeletonNode(Node* node)
    {
        string realName = AssimpCommon.StripPivotSuffix(node->MName.AsString);

        // Assimp stores MTransformation in column-vector convention (row-major memory).
        // Transpose -> row-vector convention (System.Numerics).
        Matrix4x4 combinedLocal = Matrix4x4.Transpose(node->MTransformation);
        var realChildren = new List<SkeletonNodeData>();

        for (uint i = 0; i < node->MNumChildren; i++)
        {
            Node* child = node->MChildren[i];
            string childReal = AssimpCommon.StripPivotSuffix(child->MName.AsString);

            if (childReal == realName)
                combinedLocal = AbsorbPivotChain(child, combinedLocal, realChildren);
            else
                realChildren.Add(BuildSkeletonNode(child));
        }

        var sk = new SkeletonNodeData { Name = realName, LocalTransform = combinedLocal };
        sk.Children.AddRange(realChildren);
        return sk;
    }

    /// <summary>
    /// Absorbs a chain of FBX pivot helper nodes (all sharing the same real bone name) into one local transform.
    /// In row-vector convention each successive pivot is RIGHT-appended: accumulated = accumulated * pivotLocal.
    /// (Left-prepending reverses the order and mirrors limbs on animations that use the full 3-axis range, e.g. jump.)
    /// </summary>
    private static unsafe Matrix4x4 AbsorbPivotChain(Node* pivotNode, Matrix4x4 accumulated, List<SkeletonNodeData> realChildren)
    {
        string pivotReal = AssimpCommon.StripPivotSuffix(pivotNode->MName.AsString);
        Matrix4x4 pivotLocal = Matrix4x4.Transpose(pivotNode->MTransformation);
        accumulated = accumulated * pivotLocal;

        for (uint i = 0; i < pivotNode->MNumChildren; i++)
        {
            Node* child = pivotNode->MChildren[i];
            string childReal = AssimpCommon.StripPivotSuffix(child->MName.AsString);

            if (childReal == pivotReal)
                accumulated = AbsorbPivotChain(child, accumulated, realChildren);
            else
                realChildren.Add(BuildSkeletonNode(child));
        }
        return accumulated;
    }

    private static void ScaleSkeletonTranslations(SkeletonNodeData node, float scale)
    {
        var m = node.LocalTransform;
        m.M41 *= scale;
        m.M42 *= scale;
        m.M43 *= scale;
        node.LocalTransform = m;
        foreach (var child in node.Children) ScaleSkeletonTranslations(child, scale);
    }
}
