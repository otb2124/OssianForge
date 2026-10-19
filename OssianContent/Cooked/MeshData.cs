using OssianForge.Content.Cooked;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OssianForge.Content.Meshes;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BoneWeightEntry
{
    /// <summary>Flat VBO index (after face unrolling).</summary>
    public int VertexIndex;
    public float Weight;
}

public sealed class BoneInfo
{
    public string Name = "";
    public Matrix4x4 OffsetMatrix;
    public BoneWeightEntry[] Weights = Array.Empty<BoneWeightEntry>();
}

public sealed class SubMeshData
{
    public int MaterialIndex;
    /// <summary>8 floats per vertex: position(3), normal(3), uv(2). Unit scale already applied.</summary>
    public float[] Vertices = Array.Empty<float>();
    public BoneInfo[] Bones = Array.Empty<BoneInfo>();
}

public sealed class SkeletonNodeData
{
    public string Name = "";
    public Matrix4x4 LocalTransform;
    public List<SkeletonNodeData> Children = new();
}

public sealed class MeshData
{
    public SkeletonNodeData? Root;
    public SubMeshData[] SubMeshes = Array.Empty<SubMeshData>();
}

/// <summary>Cooked mesh format "OMSH" v1: same float[] layout the renderer already consumes.</summary>
public static class MeshCodec
{
    public const uint Magic = 0x48534D4F; // 'O','M','S','H'
    public const ushort Version = 1;

    public static byte[] Write(MeshData mesh)
    {
        var w = new BlobWriter(1 << 16);
        w.Header(Magic, Version);

        w.U8(mesh.Root != null ? (byte)1 : (byte)0);
        if (mesh.Root != null) WriteNode(w, mesh.Root);

        w.I32(mesh.SubMeshes.Length);
        foreach (var sm in mesh.SubMeshes)
        {
            w.I32(sm.MaterialIndex);
            w.Arr<float>(sm.Vertices);
            w.I32(sm.Bones.Length);
            foreach (var b in sm.Bones)
            {
                w.Str(b.Name);
                w.Struct(b.OffsetMatrix);
                w.Arr<BoneWeightEntry>(b.Weights);
            }
        }
        return w.ToArray();
    }

    public static MeshData Read(byte[] blob)
    {
        var r = new BlobReader(blob);
        r.ExpectHeader(Magic, Version, "mesh");

        var mesh = new MeshData();
        if (r.U8() != 0) mesh.Root = ReadNode(r);

        int subCount = r.I32();
        mesh.SubMeshes = new SubMeshData[subCount];
        for (int i = 0; i < subCount; i++)
        {
            var sm = new SubMeshData
            {
                MaterialIndex = r.I32(),
                Vertices = r.Arr<float>()
            };
            int boneCount = r.I32();
            sm.Bones = new BoneInfo[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                sm.Bones[b] = new BoneInfo
                {
                    Name = r.Str(),
                    OffsetMatrix = r.Struct<Matrix4x4>(),
                    Weights = r.Arr<BoneWeightEntry>()
                };
            }
            mesh.SubMeshes[i] = sm;
        }
        return mesh;
    }

    private static void WriteNode(BlobWriter w, SkeletonNodeData n)
    {
        w.Str(n.Name);
        w.Struct(n.LocalTransform);
        w.I32(n.Children.Count);
        foreach (var c in n.Children) WriteNode(w, c);
    }

    private static SkeletonNodeData ReadNode(BlobReader r)
    {
        var n = new SkeletonNodeData { Name = r.Str(), LocalTransform = r.Struct<Matrix4x4>() };
        int count = r.I32();
        for (int i = 0; i < count; i++) n.Children.Add(ReadNode(r));
        return n;
    }
}
