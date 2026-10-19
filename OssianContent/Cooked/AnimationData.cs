using OssianForge.Content.Cooked;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OssianForge.Content.Animations;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3Key
{
    public double Time;
    public Vector3 Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QuaternionKey
{
    public double Time;
    public Quaternion Value;
}

public sealed class BoneChannelData
{
    public string BoneName = "";
    public Vector3Key[] PositionKeys = Array.Empty<Vector3Key>();
    public QuaternionKey[] RotationKeys = Array.Empty<QuaternionKey>();
    public Vector3Key[] ScaleKeys = Array.Empty<Vector3Key>();
}

public sealed class AnimationClipData
{
    public string Name = "";
    public double DurationTicks;
    public double TicksPerSecond;
    public BoneChannelData[] Channels = Array.Empty<BoneChannelData>();

    public double DurationSeconds => DurationTicks / (TicksPerSecond > 0 ? TicksPerSecond : 30.0);
}

public sealed class AnimationData
{
    public AnimationClipData[] Clips = Array.Empty<AnimationClipData>();
}

/// <summary>Cooked animation format "OANM" v1. Clip filtering, TPS fix, pivot stripping and unit scale are already applied.</summary>
public static class AnimationCodec
{
    public const uint Magic = 0x4D4E414F; // 'O','A','N','M'
    public const ushort Version = 1;

    public static byte[] Write(AnimationData data)
    {
        var w = new BlobWriter(1 << 16);
        w.Header(Magic, Version);
        w.I32(data.Clips.Length);
        foreach (var clip in data.Clips)
        {
            w.Str(clip.Name);
            w.F64(clip.DurationTicks);
            w.F64(clip.TicksPerSecond);
            w.I32(clip.Channels.Length);
            foreach (var ch in clip.Channels)
            {
                w.Str(ch.BoneName);
                w.Arr<Vector3Key>(ch.PositionKeys);
                w.Arr<QuaternionKey>(ch.RotationKeys);
                w.Arr<Vector3Key>(ch.ScaleKeys);
            }
        }
        return w.ToArray();
    }

    public static AnimationData Read(byte[] blob)
    {
        var r = new BlobReader(blob);
        r.ExpectHeader(Magic, Version, "animation");

        var data = new AnimationData { Clips = new AnimationClipData[r.I32()] };
        for (int i = 0; i < data.Clips.Length; i++)
        {
            var clip = new AnimationClipData
            {
                Name = r.Str(),
                DurationTicks = r.F64(),
                TicksPerSecond = r.F64()
            };
            clip.Channels = new BoneChannelData[r.I32()];
            for (int c = 0; c < clip.Channels.Length; c++)
            {
                clip.Channels[c] = new BoneChannelData
                {
                    BoneName = r.Str(),
                    PositionKeys = r.Arr<Vector3Key>(),
                    RotationKeys = r.Arr<QuaternionKey>(),
                    ScaleKeys = r.Arr<Vector3Key>()
                };
            }
            data.Clips[i] = clip;
        }
        return data;
    }
}
