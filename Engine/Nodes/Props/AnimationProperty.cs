using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.MeshFiles;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class AnimationProperty : NodeProperty
    {
        public AnimationResource AnimationResource { get; private set; }
        public Matrix4x4[] BonePalette { get; private set; } = Array.Empty<Matrix4x4>();

        public AnimationProperty(string animationResourceId)
        {
            AnimationResource = Engine.Resources.GetResource(animationResourceId) as AnimationResource
                ?? throw new Exception($"AnimationResource not found: '{animationResourceId}'");
        }

        public void Play(string clipName, bool loop = true) => AnimationResource.Play(clipName, loop);
        public void Play(int clipIndex, bool loop = true) => AnimationResource.Play(clipIndex, loop);
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

        private void WalkSkeleton(SkeletonNode node, Matrix4x4 parentTransform,
                                   List<BoneData> bones, Matrix4x4[] palette, ref int matched)
        {
            Matrix4x4? animated = AnimationResource.TryGetBoneTransform(node.Name);
            Matrix4x4 local = animated ?? node.LocalTransform;

            // --- CONVENTION PROOF ---
            // System.Numerics uses ROW-VECTOR convention: v' = v * M.
            // Uploading a System.Numerics matrix with UniformMatrix4(transpose=false)
            // causes OpenGL to see it transposed (because SN is row-major, GL is column-major).
            // GLSL then does mat*vec, which is (SN_matrix^T)^T * v = SN_matrix * v.
            // Net result: GLSL mat*vec == C# v*mat. The two conventions are consistent.
            //
            // Global transform in column-vector:
            //   G_child_col = G_parent_col * L_child_col
            //
            // Transposing both sides (col→row):
            //   G_child_rv = (G_parent_col * L_child_col)^T
            //              = L_child_col^T * G_parent_col^T
            //              = L_child_rv  *  G_parent_rv
            //
            // Therefore in row-vector: global = local * parentTransform
            Matrix4x4 global = local * parentTransform;

            int idx = bones.FindIndex(b => b.Name == node.Name);
            if (idx >= 0)
            {
                // Skinning formula derivation (column-vector standard):
                //   v_skinned_col = AnimGlobal_col * OffsetMatrix_col * v_col
                //
                // In row-vector: palette_rv = (AnimGlobal_col * OffsetMatrix_col)^T
                //                           = OffsetMatrix_col^T * AnimGlobal_col^T
                //                           = OffsetMatrix_rv   * AnimGlobal_rv
                //                           = OffsetMatrix_rv   * global
                //
                // At bind pose: global == BindGlobal_rv, and OffsetMatrix_rv == inverse(BindGlobal_rv)
                // so palette == Identity — vertices stay exactly where they are. ✓
                palette[idx] = bones[idx].OffsetMatrix * global;
                matched++;
            }

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
    }
}