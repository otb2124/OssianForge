using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Resources.Animations
{
    public class AnimationResource : Resource
    {
        public List<AnimationClip> Clips = new();

        public AnimationClip CurrentClip { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLooping { get; private set; }
        public double CurrentTime { get; private set; }

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

        public void Stop() { IsPlaying = false; CurrentTime = 0; }
        public void Pause() { IsPlaying = false; }
        public void Resume() { if (CurrentClip != null) IsPlaying = true; }

        public void Update(double deltaTime)
        {
            //Console.WriteLine($"[ANIM] delta={deltaTime:F4} time={CurrentTime:F2}");

            if (!IsPlaying || CurrentClip == null) return;

            // Clamp delta to a max of one frame at 30fps (0.0333s) so that
            // frame-rate spikes or a mismatched update rate can't over-advance the clock.
            // Also guards against the Silk.NET "catch-up" double-tick on high refresh monitors.
            const double maxDelta = 1.0 / 30.0;
            deltaTime = Math.Min(deltaTime, maxDelta);

            double tps = CurrentClip.TicksPerSecond > 0 ? CurrentClip.TicksPerSecond : 30.0;
            CurrentTime += deltaTime * tps;

            if (CurrentTime >= CurrentClip.DurationTicks)
            {
                if (IsLooping) CurrentTime %= CurrentClip.DurationTicks;
                else Stop();
            }
        }

        public Matrix4x4 GetBoneTransform(string boneName)
        {
            if (CurrentClip == null) return Matrix4x4.Identity;
            var ch = CurrentClip.Channels.Find(c => c.BoneName == boneName);
            return ch == null ? Matrix4x4.Identity : BuildLocalMatrix(ch);
        }

        public Matrix4x4? TryGetBoneTransform(string boneName)
        {
            if (CurrentClip == null) return null;
            var ch = CurrentClip.Channels.Find(c => c.BoneName == boneName);
            return ch == null ? null : BuildLocalMatrix(ch);
        }

        // System.Numerics memory layout is identical to OpenGL column-major layout —
        // both CreateScale/CreateFromQuaternion/CreateTranslation and Assimp nodes
        // after Transpose() end up in the same layout. No extra transpose needed here.
        private Matrix4x4 BuildLocalMatrix(BoneChannel ch)
        {
            var T = InterpolatePosition(ch);
            var R = InterpolateRotation(ch);
            var S = InterpolateScale(ch);

            // Row-vector convention: S then R then T (applied left-to-right).
            // This matches what Assimp decomposed and what MeshFile.LocalTransform
            // was storing after its Transpose() call.
            return Matrix4x4.CreateScale(S)
                 * Matrix4x4.CreateFromQuaternion(R)
                 * Matrix4x4.CreateTranslation(T);
        }

        private Vector3 InterpolatePosition(BoneChannel ch)
        {
            if (ch.PositionKeys.Count == 0) return Vector3.Zero;
            if (ch.PositionKeys.Count == 1) return ch.PositionKeys[0].Value;
            int i = FindKeyIndex(ch.PositionKeys, CurrentTime);
            int n = Math.Min(i + 1, ch.PositionKeys.Count - 1);
            return Vector3.Lerp(ch.PositionKeys[i].Value, ch.PositionKeys[n].Value,
                                GetFactor(ch.PositionKeys[i].Time, ch.PositionKeys[n].Time));
        }

        private Quaternion InterpolateRotation(BoneChannel ch)
        {
            if (ch.RotationKeys.Count == 0) return Quaternion.Identity;
            if (ch.RotationKeys.Count == 1) return ch.RotationKeys[0].Value;
            int i = FindKeyIndex(ch.RotationKeys, CurrentTime);
            int n = Math.Min(i + 1, ch.RotationKeys.Count - 1);
            return Quaternion.Slerp(ch.RotationKeys[i].Value, ch.RotationKeys[n].Value,
                                    GetFactor(ch.RotationKeys[i].Time, ch.RotationKeys[n].Time));
        }

        private Vector3 InterpolateScale(BoneChannel ch)
        {
            if (ch.ScaleKeys.Count == 0) return Vector3.One;
            if (ch.ScaleKeys.Count == 1) return ch.ScaleKeys[0].Value;
            int i = FindKeyIndex(ch.ScaleKeys, CurrentTime);
            int n = Math.Min(i + 1, ch.ScaleKeys.Count - 1);
            return Vector3.Lerp(ch.ScaleKeys[i].Value, ch.ScaleKeys[n].Value,
                                GetFactor(ch.ScaleKeys[i].Time, ch.ScaleKeys[n].Time));
        }

        private int FindKeyIndex<T>(List<T> keys, double time) where T : VectorKey
        {
            for (int i = 0; i < keys.Count - 1; i++)
                if (time < keys[i + 1].Time) return i;
            return keys.Count - 2;
        }

        private int FindKeyIndex(List<QuatKey> keys, double time)
        {
            for (int i = 0; i < keys.Count - 1; i++)
                if (time < keys[i + 1].Time) return i;
            return keys.Count - 2;
        }

        private float GetFactor(double lastTime, double nextTime)
        {
            double d = nextTime - lastTime;
            if (d <= 0) return 0f;
            return Math.Clamp((float)((CurrentTime - lastTime) / d), 0f, 1f);
        }
    }
}