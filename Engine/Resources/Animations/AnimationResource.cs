using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Resources.Animations
{
    public class AnimationResource : Resource
    {
        // All clips loaded from the provided animation files
        public List<AnimationClip> Clips = new();

        // Playback state
        public AnimationClip CurrentClip { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLooping { get; private set; }
        public double CurrentTime { get; private set; } // in ticks

        private readonly string[] _animationFileIds;

        public AnimationResource(string id, params string[] animationFileIds)
        {
            Id = id;
            _animationFileIds = animationFileIds;
        }

        public override void Load()
        {
            foreach (var fileId in _animationFileIds)
            {
                var animFile = Engine.Resources.GetResourceFile(fileId) as AnimationFile
                    ?? throw new Exception($"AnimationFile not found: '{fileId}'");

                Clips.AddRange(animFile.Clips);
            }
        }

        // --- Playback controls ---

        public void Play(string clipName, bool loop = true)
        {
            var clip = Clips.Find(c => c.Name == clipName)
                ?? throw new Exception($"AnimationResource clip not found: '{clipName}'");

            CurrentClip = clip;
            CurrentTime = 0;
            IsPlaying = true;
            IsLooping = loop;
        }

        public void Play(int clipIndex, bool loop = true)
        {
            if (clipIndex < 0 || clipIndex >= Clips.Count)
                throw new IndexOutOfRangeException($"Clip index {clipIndex} out of range.");

            CurrentClip = Clips[clipIndex];
            CurrentTime = 0;
            IsPlaying = true;
            IsLooping = loop;
        }

        public void Stop()
        {
            IsPlaying = false;
            CurrentTime = 0;
        }

        public void Pause() => IsPlaying = false;

        public void Resume()
        {
            if (CurrentClip != null)
                IsPlaying = true;
        }

        // Call this every frame with delta time in seconds
        public void Update(double deltaTime)
        {
            if (!IsPlaying || CurrentClip == null) return;

            double ticksPerSecond = CurrentClip.TicksPerSecond > 0 ? CurrentClip.TicksPerSecond : 25.0;
            CurrentTime += deltaTime * ticksPerSecond;

            if (CurrentTime >= CurrentClip.DurationTicks)
            {
                if (IsLooping)
                    CurrentTime %= CurrentClip.DurationTicks;
                else
                    Stop();
            }
        }

        // --- Sampling ---

        // Returns the interpolated local transform matrix for a given bone at CurrentTime
        public Matrix4x4 GetBoneTransform(string boneName)
        {
            if (CurrentClip == null) return Matrix4x4.Identity;

            var channel = CurrentClip.Channels.Find(c => c.BoneName == boneName);
            if (channel == null) return Matrix4x4.Identity;

            var position = InterpolatePosition(channel);
            var rotation = InterpolateRotation(channel);
            var scale = InterpolateScale(channel);

            return Matrix4x4.CreateScale(scale)
                 * Matrix4x4.CreateFromQuaternion(rotation)
                 * Matrix4x4.CreateTranslation(position);
        }

        // --- Interpolation helpers ---

        private Vector3 InterpolatePosition(BoneChannel channel)
        {
            if (channel.PositionKeys.Count == 1)
                return channel.PositionKeys[0].Value;

            int i = FindKeyIndex(channel.PositionKeys, CurrentTime);
            int next = Math.Min(i + 1, channel.PositionKeys.Count - 1);

            float t = GetFactor(channel.PositionKeys[i].Time, channel.PositionKeys[next].Time);
            return Vector3.Lerp(channel.PositionKeys[i].Value, channel.PositionKeys[next].Value, t);
        }

        private Quaternion InterpolateRotation(BoneChannel channel)
        {
            if (channel.RotationKeys.Count == 1)
                return channel.RotationKeys[0].Value;

            int i = FindKeyIndex(channel.RotationKeys, CurrentTime);
            int next = Math.Min(i + 1, channel.RotationKeys.Count - 1);

            float t = GetFactor(channel.RotationKeys[i].Time, channel.RotationKeys[next].Time);
            return Quaternion.Slerp(channel.RotationKeys[i].Value, channel.RotationKeys[next].Value, t);
        }

        private Vector3 InterpolateScale(BoneChannel channel)
        {
            if (channel.ScaleKeys.Count == 1)
                return channel.ScaleKeys[0].Value;

            int i = FindKeyIndex(channel.ScaleKeys, CurrentTime);
            int next = Math.Min(i + 1, channel.ScaleKeys.Count - 1);

            float t = GetFactor(channel.ScaleKeys[i].Time, channel.ScaleKeys[next].Time);
            return Vector3.Lerp(channel.ScaleKeys[i].Value, channel.ScaleKeys[next].Value, t);
        }

        private int FindKeyIndex<T>(List<T> keys, double time) where T : VectorKey
        {
            for (int i = 0; i < keys.Count - 1; i++)
                if (time < keys[i + 1].Time) return i;
            return keys.Count - 2;
        }

        // Overload for QuatKey since it doesn't share a base with VectorKey
        private int FindKeyIndex(List<QuatKey> keys, double time)
        {
            for (int i = 0; i < keys.Count - 1; i++)
                if (time < keys[i + 1].Time) return i;
            return keys.Count - 2;
        }

        private float GetFactor(double lastTime, double nextTime)
        {
            double delta = nextTime - lastTime;
            if (delta <= 0) return 0f;
            return (float)((CurrentTime - lastTime) / delta);
        }
    }
}