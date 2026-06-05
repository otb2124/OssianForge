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

        public double DurationSeconds => DurationTicks / (TicksPerSecond > 0 ? TicksPerSecond : 25.0);
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
                // Use zero post-process flags for animation-only files — we only need
                // the raw animation data, no mesh processing at all.
                var scene = assimp.ImportFile(globalPath, 0);

                if (scene == null || scene->MRootNode == null)
                {
                    string error = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load animation: {globalPath}\nAssimp error: {error}");
                }

                if (scene->MNumAnimations == 0)
                    throw new Exception($"No animations found in file: {globalPath}");

                // Read the same UnitScaleFactor that MeshFile reads so animation
                // position keyframe values are in the same units as vertex positions (meters).
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

                Console.WriteLine($"[ANIM] Loading '{globalPath}': {scene->MNumAnimations} clip(s), unitScale={unitScale}");

                for (uint a = 0; a < scene->MNumAnimations; a++)
                {
                    var anim = scene->MAnimations[a];

                    string rawName = anim->MName.AsString;
                    string clipName = rawName.Contains('|')
                        ? rawName.Split('|')[1]  // strip "Armature|" prefix
                        : rawName;

                    var clip = new AnimationClip
                    {
                        Name = clipName,
                        DurationTicks = anim->MDuration,
                        TicksPerSecond = anim->MTicksPerSecond
                    };

                    Console.WriteLine($"[ANIM]   Clip '{clipName}': {anim->MNumChannels} channels, " +
                                      $"{anim->MDuration} ticks @ {anim->MTicksPerSecond} tps");

                    // When multiple pivot channels collapse to the same bone name,
                    // we merge their keys into one BoneChannel rather than creating
                    // duplicates (which would make FindIndex return the first one and
                    // silently ignore the rest).
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

                        // Position keys — scale value to meters to match vertex positions.
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

                        // Rotation keys
                        for (uint k = 0; k < channel->MNumRotationKeys; k++)
                        {
                            var key = channel->MRotationKeys[k];
                            boneChannel.RotationKeys.Add(new QuatKey
                            {
                                Time = key.MTime,
                                // Assimp quaternion layout: (w, x, y, z) — System.Numerics: (x, y, z, w)
                                Value = new Quaternion(key.MValue.X, key.MValue.Y, key.MValue.Z, key.MValue.W)
                            });
                        }

                        // Scale keys (dimensionless — no unit conversion needed)
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

                    // After merging pivot sub-channels into one BoneChannel the key lists may
                    // contain entries from multiple source channels interleaved in arbitrary order.
                    // FindKeyIndex uses a linear time-ordered scan, so keys MUST be sorted.
                    foreach (var ch in channelMap.Values)
                    {
                        ch.PositionKeys.Sort((a2, b2) => a2.Time.CompareTo(b2.Time));
                        ch.RotationKeys.Sort((a2, b2) => a2.Time.CompareTo(b2.Time));
                        ch.ScaleKeys.Sort((a2, b2) => a2.Time.CompareTo(b2.Time));
                    }

                    clip.Channels.AddRange(channelMap.Values);
                    Clips.Add(clip);
                }

                assimp.FreeScene(scene);
            }
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