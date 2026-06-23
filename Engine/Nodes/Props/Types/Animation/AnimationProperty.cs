using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.MeshFiles;
using Silk.NET.Assimp;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class AnimationProperty : NodeProperty
    {
        public AnimationResource AnimationResource { get; private set; }
        public Matrix4x4[] BonePalette { get; private set; } = Array.Empty<Matrix4x4>();
        public Vector3 RootMotionDelta { get; private set; } = Vector3.Zero;

        // The root bone name — set this to match your skeleton's root
        // (e.g. "Hips", "mixamorig:Hips", "Root", etc.)
        public string RootBoneName { get; set; } = "mixamorig:Hips";

        private Vector3 _lastRootPosition = Vector3.Zero;
        public bool ApplyRootMotion = false;


        public AnimationProperty(string animationResourceId)
        {
            AnimationResource = Engine.Resources.GetResource<AnimationResource>(animationResourceId)
                ?? throw new Exception($"AnimationResource not found: '{animationResourceId}'");
        }

        public void Play(string clipName, bool loop = true, float speed = 1f)
            => AnimationResource.Play(clipName, loop, speed);

        public void Play(int clipIndex, bool loop = true, float speed = 1f)
            => AnimationResource.Play(clipIndex, loop, speed);

        public void SetSpeed(float speed) => AnimationResource.SetSpeed(speed);
        public void Stop() => AnimationResource.Stop();
        public void Pause() => AnimationResource.Pause();
        public void Resume() => AnimationResource.Resume();

        public bool IsPlaying => AnimationResource.IsPlaying;
        public AnimationClip CurrentClip => AnimationResource.CurrentClip;
        public double CurrentTime => AnimationResource.CurrentTime;

        public override void OnUpdate(Node node, double delta)
        {
            AnimationResource.Update(delta);

            var mesh = node.GetProperty<MeshProperty>();
            if (mesh == null || AnimationResource.CurrentClip == null) return;

            var skeleton = mesh.MeshResource.Skeleton;
            if (skeleton == null) return;

            var allBones = mesh.MeshResource.AllBones;

            BonePalette = new Matrix4x4[allBones.Count];
            for (int i = 0; i < BonePalette.Length; i++)
                BonePalette[i] = Matrix4x4.Identity;

            int matched = 0;
            WalkSkeleton(skeleton, Matrix4x4.Identity, allBones, BonePalette, ref matched);
        }


        private void ExtractRootMotion()
        {
            var channel = AnimationResource.CurrentClip?.Channels
                .Find(c => c.BoneName == RootBoneName);

            if (channel == null)
            {
                RootMotionDelta = Vector3.Zero;
                return;
            }

            // Get current root position from animation
            Vector3 currentRootPos = SamplePosition(channel, AnimationResource.CurrentTime);

            RootMotionDelta = currentRootPos - _lastRootPosition;
            _lastRootPosition = currentRootPos;

            // Zero out root bone position keys so it doesn't move the mesh away from origin
            // We do this by overriding TryGetBoneTransform result in WalkSkeleton via a flag,
            // or more cleanly — neutralise position in the channel on the fly in WalkSkeleton
        }

        private Vector3 SamplePosition(BoneChannel ch, double time)
        {
            if (ch.PositionKeys.Count == 0) return Vector3.Zero;
            if (ch.PositionKeys.Count == 1) return ch.PositionKeys[0].Value;

            int i = 0;
            for (; i < ch.PositionKeys.Count - 1; i++)
                if (time < ch.PositionKeys[i + 1].Time) break;

            int n = Math.Min(i + 1, ch.PositionKeys.Count - 1);
            double d = ch.PositionKeys[n].Time - ch.PositionKeys[i].Time;
            float t = d <= 0 ? 0f : Math.Clamp((float)((time - ch.PositionKeys[i].Time) / d), 0f, 1f);

            return Vector3.Lerp(ch.PositionKeys[i].Value, ch.PositionKeys[n].Value, t);
        }


        private void WalkSkeleton(SkeletonNode node, Matrix4x4 parentTransform,
                   List<BoneData> bones, Matrix4x4[] palette, ref int matched)
        {
            Matrix4x4? animated = AnimationResource.TryGetBoneTransform(node.Name);
            Matrix4x4 local = animated ?? node.LocalTransform;

            if (node.Name == RootBoneName && animated.HasValue)
            {
                Matrix4x4.Decompose(local, out var scale, out var rot, out _);
                local = Matrix4x4.CreateScale(scale)
                      * Matrix4x4.CreateFromQuaternion(rot);
            }

            Matrix4x4 global = local * parentTransform;

            int idx = bones.FindIndex(b => b.Name == node.Name);
            if (idx >= 0)
            {
                palette[idx] = bones[idx].OffsetMatrix * global;
                matched++;
            }

            // Always pass global to children — whether or not this node is a bone.
            // Intermediate structural nodes still contribute their transform to the chain.
            foreach (var child in node.Children)
                WalkSkeleton(child, global, bones, palette, ref matched);
        }

        public Matrix4x4 GetBoneTransform(string boneName)
            => AnimationResource.GetBoneTransform(boneName);

        private static SkeletonNode FindSkeletonNode(SkeletonNode root, string name)
        {
            if (root.Name == name) return root;
            foreach (var child in root.Children)
            {
                var found = FindSkeletonNode(child, name);
                if (found != null) return found;
            }
            return null;
        }


        public (Vector3 min, Vector3 max) GetAnimatedWorldBounds(Matrix4x4 worldMatrix)
        {
            if (BonePalette == null || BonePalette.Length == 0)
                return (Vector3.Zero, Vector3.Zero);

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var bone in BonePalette)
            {
                // Each palette entry = offsetMatrix * globalTransform
                // Extract the translation (bone world position approximation)
                var pos = Vector3.Transform(Vector3.Zero, bone * worldMatrix);
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }

            return (min, max);
        }


        //TODO: something strange here in params...
        public Matrix4x4[] GetPalette(MeshProperty mesh, SubMeshResource subMesh)
        {
            return GetPalette(mesh.MeshResource, subMesh);
        }

        public Matrix4x4[] GetPalette(MeshResource meshResource, SubMeshResource subMesh)
        {
            Matrix4x4[] palette = null;
            if (BonePalette != null && BonePalette.Length > 0
                && meshResource.AllBones != null)
            {
                // Build a palette in THIS submesh's bone order
                palette = new Matrix4x4[subMesh.Bones.Count];
                for (int i = 0; i < subMesh.Bones.Count; i++)
                {
                    int unifiedIdx = meshResource.AllBones
                        .FindIndex(b => b.Name == subMesh.Bones[i].Name);
                    palette[i] = unifiedIdx >= 0
                        ? BonePalette[unifiedIdx]
                        : Matrix4x4.Identity;
                }
            }

            return palette;
        }

        // Helpers to decompose matrix back to components
        private static Vector3 GetScale(Matrix4x4 m)
        {
            return new Vector3(
                new Vector3(m.M11, m.M12, m.M13).Length(),
                new Vector3(m.M21, m.M22, m.M23).Length(),
                new Vector3(m.M31, m.M32, m.M33).Length());
        }

        private static Quaternion GetRotation(Matrix4x4 m)
        {
            Matrix4x4.Decompose(m, out _, out var rot, out _);
            return rot;
        }
    }
}