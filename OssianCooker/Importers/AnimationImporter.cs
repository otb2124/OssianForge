using OssianForge.Content;
using OssianForge.Content.Animations;
using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssimpAnimation = Silk.NET.Assimp.Animation;
using Math = System.Math;

namespace OssianForge.Cooker.Importers;

/// <summary>
/// Port of AnimationFile.Load(). Clip filtering, tick-rate fix, pivot stripping and unit scale happen here once.
/// Clip selection is explicit: sidecar "clip" ("*" = all), defaulting to the file-name stem as the engine does today.
/// </summary>
internal sealed class AnimationImporter : IImporter
{
    public AssetType Type => AssetType.Animation;
    public int Version => 1;

    public byte[] Import(ImportContext ctx)
    {
        AnimationData data;
        lock (AssimpCommon.Lock) data = ImportScene(ctx);
        return AnimationCodec.Write(data);
    }

    private static unsafe AnimationData ImportScene(ImportContext ctx)
    {
        string stem = Path.GetFileNameWithoutExtension(ctx.FullPath);
        string wanted = ctx.Settings.Clip ?? stem;
        bool keepAll = wanted == "*";

        using var assimp = Assimp.GetApi();
        var scene = assimp.ImportFile(ctx.FullPath, 0);

        if (scene == null || scene->MRootNode == null)
            throw new Exception($"Failed to load animation.\nAssimp error: {assimp.GetErrorStringS()}");

        try
        {
            if (scene->MNumAnimations == 0)
                throw new Exception("No animations found in file.");

            float unitScale;
            if (ctx.Settings.UnitScale is { } forced)
            {
                unitScale = forced;
            }
            else
            {
                unitScale = AssimpCommon.ReadUnitScaleFactor(scene);

                // Safety net: no metadata, check first position key magnitude.
                if (unitScale == 1f)
                {
                    var anim0 = scene->MAnimations[0];
                    if (anim0->MNumChannels > 0 && anim0->MChannels[0]->MNumPositionKeys > 0)
                    {
                        var v = anim0->MChannels[0]->MPositionKeys[0].MValue;
                        float mag = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
                        if (mag > 10f) unitScale = 0.01f;
                    }
                }
            }

            var clips = new List<AnimationClipData>();
            var available = new List<string>();

            for (uint a = 0; a < scene->MNumAnimations; a++)
            {
                var anim = scene->MAnimations[a];
                string rawName = anim->MName.AsString;

                // Blender/Mixamo action names arrive as "Armature|clip" or "Armature|Armature|clip": take the last segment.
                string clipName = rawName.Contains('|') ? rawName.Split('|')[^1] : rawName;
                available.Add(clipName);

                if (!keepAll && !clipName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug($"{ctx.RelPath}: skipping clip '{clipName}' (wanted '{wanted}')");
                    continue;
                }

                double rawTps = anim->MTicksPerSecond > 0 ? anim->MTicksPerSecond : 30.0;
                double tps = ResolveTps(anim, rawTps);

                var clip = new AnimationClipData
                {
                    Name = clipName,
                    DurationTicks = anim->MDuration,
                    TicksPerSecond = tps
                };

                var channelMap = new Dictionary<string, BoneChannelBuilder>();
                var order = new List<BoneChannelBuilder>();

                for (uint c = 0; c < anim->MNumChannels; c++)
                {
                    var channel = anim->MChannels[c];
                    string boneName = AssimpCommon.StripPivotSuffix(channel->MNodeName.AsString);

                    if (!channelMap.TryGetValue(boneName, out var bc))
                    {
                        bc = new BoneChannelBuilder { BoneName = boneName };
                        channelMap[boneName] = bc;
                        order.Add(bc);
                    }

                    for (uint k = 0; k < channel->MNumPositionKeys; k++)
                    {
                        var key = channel->MPositionKeys[k];
                        bc.Pos.Add(new Vector3Key
                        {
                            Time = key.MTime,
                            Value = new Vector3(key.MValue.X * unitScale, key.MValue.Y * unitScale, key.MValue.Z * unitScale)
                        });
                    }

                    for (uint k = 0; k < channel->MNumRotationKeys; k++)
                    {
                        var key = channel->MRotationKeys[k];
                        bc.Rot.Add(new QuaternionKey
                        {
                            Time = key.MTime,
                            Value = new Quaternion(key.MValue.X, key.MValue.Y, key.MValue.Z, key.MValue.W)
                        });
                    }

                    for (uint k = 0; k < channel->MNumScalingKeys; k++)
                    {
                        var key = channel->MScalingKeys[k];
                        bc.Scale.Add(new Vector3Key
                        {
                            Time = key.MTime,
                            Value = new Vector3(key.MValue.X, key.MValue.Y, key.MValue.Z)
                        });
                    }
                }

                clip.Channels = order.Select(b => b.Build()).ToArray();
                clips.Add(clip);
            }

            if (clips.Count == 0)
                throw new Exception(
                    $"No clip named '{wanted}' in file. Available: {string.Join(", ", available.Select(n => $"'{n}'"))}. " +
                    $"Add a \"clip\" entry to '{Path.GetFileName(ctx.FullPath)}.import.json' (use \"*\" for all clips).");

            return new AnimationData { Clips = clips.ToArray() };
        }
        finally
        {
            assimp.FreeScene(scene);
        }
    }

    private static unsafe double ResolveTps(AssimpAnimation* anim, double reportedTps)
    {
        for (uint c = 0; c < anim->MNumChannels; c++)
        {
            var ch = anim->MChannels[c];
            if (ch->MNumRotationKeys < 2) continue;

            double durationAtReported = anim->MDuration / reportedTps;
            if (reportedTps == 60.0 && durationAtReported < 2.5)
                return 30.0;

            return reportedTps;
        }
        return reportedTps;
    }

    private sealed class BoneChannelBuilder
    {
        public string BoneName = "";
        public List<Vector3Key> Pos = new();
        public List<QuaternionKey> Rot = new();
        public List<Vector3Key> Scale = new();

        public BoneChannelData Build()
        {
            Pos.Sort((x, y) => x.Time.CompareTo(y.Time));
            Rot.Sort((x, y) => x.Time.CompareTo(y.Time));
            Scale.Sort((x, y) => x.Time.CompareTo(y.Time));
            return new BoneChannelData
            {
                BoneName = BoneName,
                PositionKeys = Pos.ToArray(),
                RotationKeys = Rot.ToArray(),
                ScaleKeys = Scale.ToArray()
            };
        }
    }
}
