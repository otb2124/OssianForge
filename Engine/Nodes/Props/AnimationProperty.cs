using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Meshes;
using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class AnimationProperty : NodeProperty
    {
        public AnimationResource AnimationResource { get; private set; }

        public AnimationProperty(string animationResourceId)
        {
            AnimationResource = Engine.Resources.GetResource(animationResourceId) as AnimationResource
                    ?? throw new Exception($"AnimationResource not found: '{animationResourceId}'");
        }

        // --- Forwarded controls ---

        public void Play(string clipName, bool loop = true)
            => AnimationResource.Play(clipName, loop);

        public void Play(int clipIndex, bool loop = true)
            => AnimationResource.Play(clipIndex, loop);

        public void Stop() => AnimationResource.Stop();
        public void Pause() => AnimationResource.Pause();
        public void Resume() => AnimationResource.Resume();

        public bool IsPlaying => AnimationResource.IsPlaying;
        public AnimationClip CurrentClip => AnimationResource.CurrentClip;
        public double CurrentTime => AnimationResource.CurrentTime;

        // --- Lifecycle ---

        public override void OnUpdate(Node node, double delta)
        {
            AnimationResource.Update(delta);
        }

        // Returns the interpolated bone transform for use in your renderer/shader
        public Matrix4x4 GetBoneTransform(string boneName)
            => AnimationResource.GetBoneTransform(boneName);
    }
}