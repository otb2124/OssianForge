using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Resources.Animations
{
    public class AnimationResource : Resource
    {
        public List<AnimationClip> Clips = new();

        private readonly string[] _animationFileIds;

        public AnimationResource(string id, params string[] animationFileIds)
        {
            Id = id;
            _animationFileIds = animationFileIds;
        }

        public override void Load()
        {
            base.Load();
            foreach (var fileId in _animationFileIds)
            {
                var animFile = Engine.Resources.GetResource<AnimationFile>(fileId)
                    ?? throw new Exception($"AnimationFile not found: '{fileId}'");
                Clips.AddRange(animFile.Clips);
            }

            // Log all loaded clip names so you know exactly what to call Play() with
            foreach (var clip in Clips)
                Console.WriteLine($"[ANIMRSOURCE] Loaded clip: '{clip.Name}'");
        }

        /// <summary>
        /// Playback state (clip, time, speed) lives in an AnimationPlayer, one per AnimationProperty.
        /// This resource is shared by every node that uses it, so it only holds the clips.
        /// </summary>
        public AnimationPlayer CreatePlayer() => new AnimationPlayer(this);
    }
}