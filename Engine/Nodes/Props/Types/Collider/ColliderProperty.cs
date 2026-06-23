using System;
using System.Collections.Generic;
using System.Numerics;
using OssianForge.Engine.Resources.Colliders;

namespace OssianForge.Engine.Nodes.Props
{
    public class ColliderProperty : NodeProperty
    {
        public bool IsTrigger;
        public Action<Node>? OnCollision;
        public ColliderResource ColliderResource;
        public WireframeMaterialProperty Material;

        public string AnimationSourceNodeId;

        public ColliderProperty(string colliderId, string animationSourceNodeId = null, bool isTrigger = false)
        {
            ColliderResource = Engine.Resources.GetResource<ColliderResource>(colliderId);
            AnimationSourceNodeId = animationSourceNodeId;
            IsTrigger = isTrigger;
            Material = new WireframeMaterialProperty(new Vector4(0f, 1f, 0f, 1f));
        }

        /*
        public override void OnRender(Node node, double delta)
        {
            var transform = node.GetProperty<TransformProperty>();
            if (transform == null || ColliderResource._source == null) return;

            // Resolve the node that owns the mesh + animation
            var sourceNode = AnimationSourceNodeId != null
                ? Engine.Nodes.NodeManager.GetNode(AnimationSourceNodeId)
                : node;

            var animation = sourceNode?.GetProperty<AnimationProperty>();
            var visMesh = sourceNode?.GetProperty<MeshProperty>()?.MeshResource;

            var materials = new List<MaterialProperty>(ColliderResource._source.SubMeshes.Count);
            for (int i = 0; i < ColliderResource._source.SubMeshes.Count; i++)
                materials.Add(Material);

            Engine.Graphics.Batch.DrawMesh(
                ColliderResource._source,
                materials,
                transform,
                subMesh => animation?.GetPalette(
                    visMesh ?? ColliderResource._source,
                    visMesh?.SubMeshes.ElementAtOrDefault(
                        ColliderResource._source.SubMeshes.IndexOf(subMesh)) ?? subMesh));
        }*/
    }
}