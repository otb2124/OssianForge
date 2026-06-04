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
                // No mesh processing needed — just import the scene for animation data
                var scene = assimp.ImportFile(globalPath,
                    (uint)(PostProcessSteps.Triangulate)); // minimal flags, we only care about animations

                if (scene == null || scene->MRootNode == null)
                {
                    string error = assimp.GetErrorStringS();
                    throw new Exception($"Failed to load animation: {globalPath}\nAssimp error: {error}");
                }

                if (scene->MNumAnimations == 0)
                    throw new Exception($"No animations found in file: {globalPath}");

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

                    for (uint c = 0; c < anim->MNumChannels; c++)
                    {
                        var channel = anim->MChannels[c];
                        var boneChannel = new BoneChannel
                        {
                            BoneName = channel->MNodeName.AsString
                        };

                        // Position keys
                        for (uint k = 0; k < channel->MNumPositionKeys; k++)
                        {
                            var key = channel->MPositionKeys[k];
                            boneChannel.PositionKeys.Add(new VectorKey
                            {
                                Time = key.MTime,
                                Value = new Vector3(key.MValue.X, key.MValue.Y, key.MValue.Z)
                            });
                        }

                        // Rotation keys
                        for (uint k = 0; k < channel->MNumRotationKeys; k++)
                        {
                            var key = channel->MRotationKeys[k];
                            boneChannel.RotationKeys.Add(new QuatKey
                            {
                                Time = key.MTime,
                                // Assimp quaternion is (w, x, y, z), System.Numerics is (x, y, z, w)
                                Value = new Quaternion(key.MValue.X, key.MValue.Y, key.MValue.Z, key.MValue.W)
                            });
                        }

                        // Scale keys
                        for (uint k = 0; k < channel->MNumScalingKeys; k++)
                        {
                            var key = channel->MScalingKeys[k];
                            boneChannel.ScaleKeys.Add(new VectorKey
                            {
                                Time = key.MTime,
                                Value = new Vector3(key.MValue.X, key.MValue.Y, key.MValue.Z)
                            });
                        }

                        clip.Channels.Add(boneChannel);
                    }

                    Clips.Add(clip);
                }

                assimp.FreeScene(scene);
            }
        }
    }
}