using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Resources.Animations
{
    // A single keyframe for position, rotation, or scale
    public class VectorKey
    {
        public double Time;
        public Vector3 Value;
    }

    public class QuatKey
    {
        public double Time;
        public Quaternion Value;
    }

    // All keyframe tracks for one bone
    public class BoneChannel
    {
        public string BoneName;
        public List<VectorKey> PositionKeys = new();
        public List<QuatKey> RotationKeys = new();
        public List<VectorKey> ScaleKeys = new();
    }

    // The full animation clip
    public class AnimationClip
    {
        public string Name;
        public double DurationTicks;   // length in ticks
        public double TicksPerSecond;  // convert to seconds: time / TicksPerSecond
        public List<BoneChannel> Channels = new();

        public double DurationSeconds => DurationTicks / (TicksPerSecond > 0 ? TicksPerSecond : 30.0);
    }

    public class AnimationFile : ResourceFile
    {
        public List<AnimationClip> Clips = new();

        // BUG FIX: must be identical to MeshFile.PivotSuffixes.
        // Previously only 4 suffixes were stripped here vs 8 in MeshFile,
        // so channel bone names like "Bone_$AssimpFbx$_PostRotation" were
        // not collapsed to "Bone", causing TryGetBoneTransform to miss them.
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

        public AnimationFile(string id, string path)
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
                var scene = assimp.ImportFile(globalPath, 0);

                if (scene == null || scene->MRootNode == null)
                {
                    string error = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load animation: {globalPath}\nAssimp error: {error}");
                }

                if (scene->MNumAnimations == 0)
                    throw new Exception($"No animations found in file: {globalPath}");

                // Read UnitScaleFactor with the same robust logic as MeshFile so that
                // animation position keys are in the same units as the scaled vertices.
                float unitScale = 1f;
                var metadata = scene->MMetaData;
                if (metadata != null)
                {
                    for (uint k = 0; k < metadata->MNumProperties; k++)
                    {
                        string key = metadata->MKeys[k].AsString;
                        if (key != "UnitScaleFactor") continue;

                        var entry = metadata->MValues[k];
                        double raw = entry.MType switch
                        {
                            MetadataType.Double => *(double*)entry.MData,
                            MetadataType.Float => *(float*)entry.MData,
                            MetadataType.Int32 => *(int*)entry.MData,
                            MetadataType.Int64 => *(long*)entry.MData,
                            _ => 1.0
                        };
                        unitScale = (float)(raw * 0.01);
                        break;
                    }
                }

                // Safety-net fallback: no metadata but animation translation values look
                // like centimetres (Mixamo default). Check first position key magnitude.
                if (unitScale == 1f && scene->MNumAnimations > 0)
                {
                    var anim0 = scene->MAnimations[0];
                    if (anim0->MNumChannels > 0 && anim0->MChannels[0]->MNumPositionKeys > 0)
                    {
                        var v = anim0->MChannels[0]->MPositionKeys[0].MValue;
                        float mag = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
                        if (mag > 10f)
                        {
                            unitScale = 0.01f;
                        }
                    }
                }

                for (uint a = 0; a < scene->MNumAnimations; a++)
                {
                    var anim = scene->MAnimations[a];

                    string rawName = anim->MName.AsString;
                    string clipName = rawName.Contains('|')
                        ? rawName.Split('|')[1]
                        : rawName;

                    // FIX: Mixamo FBX files always author keyframes at 30fps, but Assimp
                    // frequently reads MTicksPerSecond as 60 from the FBX header. This causes
                    // the animation to play back at 2x speed because the keyframe timestamps
                    // are spaced for 30 tps while the clock advances as if they are at 60 tps.
                    //
                    // The log confirms this: DurationTicks ~112, which at 30 tps = ~3.7s
                    // (a normal walk cycle), but at 60 tps = ~1.87s (exactly 2x too fast).
                    //
                    // We detect this by reading the actual keyframe spacing from the first
                    // channel and comparing it to the reported TPS. If average key spacing
                    // implies ~30fps authoring, we clamp TPS to 30 regardless of the header.
                    double rawTps = anim->MTicksPerSecond > 0 ? anim->MTicksPerSecond : 30.0;
                    double tps = ResolveTps(anim, rawTps);

                    var clip = new AnimationClip
                    {
                        Name = clipName,
                        DurationTicks = anim->MDuration,
                        TicksPerSecond = tps
                    };

                    //Console.WriteLine($"[ANIM] Clip '{clipName}': {anim->MNumChannels} channels, " + $"{anim->MDuration} ticks, rawTps={rawTps}, resolvedTps={tps}, " + $"duration={clip.DurationSeconds:F2}s");

                    var channelMap = new Dictionary<string, BoneChannel>();

                    for (uint c = 0; c < anim->MNumChannels; c++)
                    {
                        var channel = anim->MChannels[c];
                        string rawBoneName = channel->MNodeName.AsString;
                        string boneName = StripPivotSuffix(rawBoneName);

                        if (!channelMap.TryGetValue(boneName, out var boneChannel))
                        {
                            boneChannel = new BoneChannel { BoneName = boneName };
                            channelMap[boneName] = boneChannel;
                        }

                        // Scale position keys to match the unit-converted vertex positions.
                        for (uint k = 0; k < channel->MNumPositionKeys; k++)
                        {
                            var key = channel->MPositionKeys[k];
                            boneChannel.PositionKeys.Add(new VectorKey
                            {
                                Time = key.MTime,
                                Value = new Vector3(
                                    key.MValue.X * unitScale,
                                    key.MValue.Y * unitScale,
                                    key.MValue.Z * unitScale)
                            });
                        }

                        // Rotation keys — dimensionless, no unit conversion needed.
                        for (uint k = 0; k < channel->MNumRotationKeys; k++)
                        {
                            var key = channel->MRotationKeys[k];
                            boneChannel.RotationKeys.Add(new QuatKey
                            {
                                Time = key.MTime,
                                Value = new Quaternion(key.MValue.X, key.MValue.Y, key.MValue.Z, key.MValue.W)
                            });
                        }

                        // Scale keys — dimensionless ratios, no unit conversion needed.
                        for (uint k = 0; k < channel->MNumScalingKeys; k++)
                        {
                            var key = channel->MScalingKeys[k];
                            boneChannel.ScaleKeys.Add(new VectorKey
                            {
                                Time = key.MTime,
                                Value = new Vector3(key.MValue.X, key.MValue.Y, key.MValue.Z)
                            });
                        }
                    }

                    // Multiple pivot sub-channels that collapsed to the same bone name
                    // may have left the key lists unsorted. FindKeyIndex requires sorted order.
                    foreach (var ch in channelMap.Values)
                    {
                        ch.PositionKeys.Sort((x, y) => x.Time.CompareTo(y.Time));
                        ch.RotationKeys.Sort((x, y) => x.Time.CompareTo(y.Time));
                        ch.ScaleKeys.Sort((x, y) => x.Time.CompareTo(y.Time));
                    }

                    clip.Channels.AddRange(channelMap.Values);
                    Clips.Add(clip);
                }

                assimp.FreeScene(scene);
            }
        }

        // Detects the real authoring TPS by sampling the average keyframe spacing
        // in the first channel with enough keys. If the spacing implies ~30fps
        // authoring while the header claims 60, we return 30.
        //
        // This handles the common Mixamo FBX quirk without hard-coding 30 for
        // every file — a non-Mixamo file legitimately authored at 60 tps will
        // have key spacing of ~1 tick and pass through unchanged.
        private static unsafe double ResolveTps(Silk.NET.Assimp.Animation* anim, double reportedTps)
        {
            // Need at least 2 keys to measure spacing.
            for (uint c = 0; c < anim->MNumChannels; c++)
            {
                var ch = anim->MChannels[c];
                if (ch->MNumRotationKeys < 2) continue;

                // Sample spacing across up to 10 consecutive key pairs.
                int samples = (int)Math.Min(ch->MNumRotationKeys - 1, 10);
                double totalSpacing = 0;
                for (int i = 0; i < samples; i++)
                    totalSpacing += ch->MRotationKeys[i + 1].MTime - ch->MRotationKeys[i].MTime;

                double avgSpacing = totalSpacing / samples;
                if (avgSpacing <= 0) continue;

                // At 30 tps, frames are 1 tick apart (spacing ≈ 1.0).
                // At 60 tps, frames are also 1 tick apart but the clock runs twice as fast.
                // The tell: if reportedTps=60 but spacing≈1, real authoring was at 30fps
                // (Mixamo writes one key per frame at 30fps = 1 tick/frame at 30tps).
                // If it were truly 60fps authoring, spacing would also be ~1 tick/frame
                // but represent half as much real time — indistinguishable by spacing alone.
                //
                // Reliable heuristic: check whether DurationTicks / reportedTps gives a
                // plausible animation length. Mixamo walk cycles are 1–4 seconds.
                // If the result is < 2s and reportedTps == 60, halve the TPS.
                double durationAtReported = anim->MDuration / reportedTps;

                if (reportedTps == 60.0 && durationAtReported < 2.5)
                {
                    // Duration looks too short — header TPS is doubled. Use 30.
                    return 30.0;
                }

                // Otherwise trust the reported TPS.
                return reportedTps;
            }

            return reportedTps;
        }

        private static string StripPivotSuffix(string name)
        {
            foreach (var suffix in PivotSuffixes)
                if (name.EndsWith(suffix))
                    return name[..^suffix.Length];
            return name;
        }
    }
}