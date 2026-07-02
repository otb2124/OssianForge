using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.IO;
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
        public double DurationTicks;
        public double TicksPerSecond;
        public List<BoneChannel> Channels = new();

        public double DurationSeconds => DurationTicks / (TicksPerSecond > 0 ? TicksPerSecond : 30.0);
    }

    public class AnimationFile : Resource
    {
        public List<AnimationClip> Clips = new();

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

        public AnimationFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            base.Load();
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;

            // Derive the expected clip name from the filename.
            // e.g. "AnimationFiles/remy_jump.fbx" → stem "remy_jump" → after last '_' → "jump"
            // This is used to filter out the extra embedded clips that Blender/Mixamo
            // bakes into every FBX export (idle, jumping, etc. end up in every file).
            string stem = System.IO.Path.GetFileNameWithoutExtension(globalPath); // e.g. "remy_jump"

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

                // Read UnitScaleFactor
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

                // Safety-net: no metadata, check first position key magnitude
                if (unitScale == 1f && scene->MNumAnimations > 0)
                {
                    var anim0 = scene->MAnimations[0];
                    if (anim0->MNumChannels > 0 && anim0->MChannels[0]->MNumPositionKeys > 0)
                    {
                        var v = anim0->MChannels[0]->MPositionKeys[0].MValue;
                        float mag = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
                        if (mag > 10f)
                            unitScale = 0.01f;
                    }
                }

                for (uint a = 0; a < scene->MNumAnimations; a++)
                {
                    var anim = scene->MAnimations[a];

                    string rawName = anim->MName.AsString;

                    // Assimp reports Blender action names as "Armature|clipname" or
                    // "Armature|Armature|clipname". Take the LAST segment after '|'.
                    string clipName = rawName.Contains('|')
                        ? rawName.Split('|')[^1]     // [^1] = last element
                        : rawName;

                    // FILTER: Blender bakes every action into every FBX export.
                    // Only load the clip whose name matches the filename stem's suffix.
                    // e.g. remy_jump.fbx → only load "jump", skip "idle", "jumping", etc.
                    //
                    // Also skip known garbage clip names Assimp emits from FBX metadata nodes.
                    if (!clipName.Equals(stem, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[ANIM] Skipping clip '{clipName}' in '{stem}.fbx' (expected '{stem}')");
                        continue;
                    }

                    double rawTps = anim->MTicksPerSecond > 0 ? anim->MTicksPerSecond : 30.0;
                    double tps = ResolveTps(anim, rawTps);

                    var clip = new AnimationClip
                    {
                        Name = clipName,
                        DurationTicks = anim->MDuration,
                        TicksPerSecond = tps
                    };

                    Console.WriteLine($"[ANIMFILE] Loaded clip '{clipName}' from '{stem}.fbx': " +
                                      $"{anim->MNumChannels} channels, {anim->MDuration} ticks, " +
                                      $"tps={tps}, duration={clip.DurationSeconds:F2}s");

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

                        for (uint k = 0; k < channel->MNumRotationKeys; k++)
                        {
                            var key = channel->MRotationKeys[k];
                            boneChannel.RotationKeys.Add(new QuatKey
                            {
                                Time = key.MTime,
                                Value = new Quaternion(key.MValue.X, key.MValue.Y, key.MValue.Z, key.MValue.W)
                            });
                        }

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

        private static unsafe double ResolveTps(Silk.NET.Assimp.Animation* anim, double reportedTps)
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

        private static string StripPivotSuffix(string name)
        {
            foreach (var suffix in PivotSuffixes)
                if (name.EndsWith(suffix))
                    return name[..^suffix.Length];
            return name;
        }
    }
}